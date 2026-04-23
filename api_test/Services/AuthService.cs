using api_test.Data;
using api_test.Entities;
using api_test.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace api_test.Services
{
    // ===== Result returned from RegisterAsync =====
    public class RegisterInitResult
    {
        public string PendingToken { get; set; } = null!;
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly OtpService _otpService;

        // Temporary storage before email verification (Registration flow)
        // Key: pendingToken (GUID), Value: TempUser
        // Static so it survives across scoped DI instances
        private static readonly Dictionary<string, TempUser> _tempUsers = new();
        private static readonly object _lock = new(); // thread safety

        public AuthService(
            AppDbContext context,
            IConfiguration configuration,
            EmailService emailService,
            OtpService otpService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _otpService = otpService;
        }

        // ===== REGISTER =====
        // Generates a pendingToken (GUID), stores temp user keyed by it, sends OTP.
        // Returns RegisterInitResult containing the pendingToken, or null on failure.
        public async Task<RegisterInitResult?> RegisterAsync(UserDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return null;

            var email = request.Email.ToLower().Trim();

            if (await _context.Users.AnyAsync(u => u.Email == email))
                return null;

            // Generate OTP first — store the exact raw value so we can verify it precisely
            var otp = _otpService.GenerateOtp();
            var otpTrimmed = otp.Trim(); // normalize before storing and before sending

            var pendingToken = Guid.NewGuid().ToString("N"); // 32-char hex, unguessable

            var passwordHasher = new PasswordHasher<User>();
            var passwordHash = passwordHasher.HashPassword(null!, request.Password);

            var tempUser = new TempUser
            {
                Email = email,
                PasswordHash = passwordHash,
                RoleId = request.RoleId,
                // Store the plain OTP for direct comparison — avoids any BCrypt salt issues
                OtpPlain = otpTrimmed,
                OtpExpiry = DateTime.UtcNow.AddMinutes(10),
                AttemptCount = 0
            };

            lock (_lock)
            {
                // Clean up any expired entries for this email first to avoid stale data
                var staleKeys = _tempUsers
                    .Where(kv => kv.Value.Email == email || kv.Value.OtpExpiry < DateTime.UtcNow)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var k in staleKeys)
                    _tempUsers.Remove(k);

                _tempUsers[pendingToken] = tempUser;
            }

            // Send exactly the same value we stored
            await _emailService.SendOtpAsync(email, otpTrimmed);

            return new RegisterInitResult { PendingToken = pendingToken };
        }

        // ===== VERIFY OTP (Registration flow) =====
        // pendingToken is the GUID returned by RegisterAsync.
        // Looks up the temp user, verifies OTP by direct string comparison,
        // creates the real user, returns JWT on success.
        public async Task<string?> VerifyOtpAsync(string pendingToken, string otp)
        {
            if (string.IsNullOrWhiteSpace(pendingToken) || string.IsNullOrWhiteSpace(otp))
                return null;

            var otpInput = otp.Trim();

            TempUser? temp;

            lock (_lock)
            {
                if (!_tempUsers.TryGetValue(pendingToken, out temp))
                    return null; // pendingToken not found

                // Expiry check
                if (temp.OtpExpiry < DateTime.UtcNow)
                {
                    _tempUsers.Remove(pendingToken);
                    return null; // expired
                }

                // Brute-force protection: max 5 attempts before lockout
                if (temp.AttemptCount >= 5)
                {
                    _tempUsers.Remove(pendingToken);
                    return null;
                }

                // Wrong OTP: increment attempt counter but keep entry alive for retry
                if (!string.Equals(temp.OtpPlain, otpInput, StringComparison.Ordinal))
                {
                    temp.AttemptCount++;
                    return null;
                }

                // Correct — remove immediately (one-time use)
                _tempUsers.Remove(pendingToken);
            }

            // Create the real verified user
            var newUser = new User
            {
                Email = temp.Email,
                PasswordHash = temp.PasswordHash,
                RoleId = 2,
                IsEmailVerified = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            await _context.Entry(newUser).Reference(u => u.Role).LoadAsync();

            return CreateToken(newUser);
        }

        // ===== LOGIN =====
        // Plain email + password check. Returns JWT directly. NO OTP involved.
        public async Task<string?> LoginAsync(UserDto request)
        {
            var email = request.Email.ToLower().Trim();

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null || !user.IsEmailVerified)
                return null;

            var passwordHasher = new PasswordHasher<User>();
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (result == PasswordVerificationResult.Failed)
                return null;

            return CreateToken(user);
        }

        // ===== GET USER =====
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            email = email.ToLower().Trim();
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        // ===== JWT TOKEN =====
        private string CreateToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim("email", user.Email),
                new Claim(ClaimTypes.Role, user.Role.RoleName)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["AppSettings:Token"]!)
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                issuer: _configuration["AppSettings:Issuer"],
                audience: _configuration["AppSettings:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // ===== TEMP USER (Registration) =====
        private class TempUser
        {
            public string Email { get; set; } = null!;
            public string PasswordHash { get; set; } = null!;
            public int RoleId { get; set; }
            public string OtpPlain { get; set; } = null!; // stored as plain text for reliable comparison
            public DateTime OtpExpiry { get; set; }
            public int AttemptCount { get; set; }
        }
    }
}