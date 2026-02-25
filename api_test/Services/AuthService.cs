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
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;
        private readonly OtpService _otpService;

        // بيانات مؤقتة قبل التفعيل
        private static Dictionary<string, TempUser> _tempUsers = new();

        public AuthService(AppDbContext context, IConfiguration configuration, EmailService emailService, OtpService otpService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
            _otpService = otpService;
        }

        // ===== REGISTER =====
        public async Task<bool> RegisterAsync(UserDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return false;

            var email = request.Email.ToLower().Trim();

            if (await _context.Users.AnyAsync(u => u.Email == email))
                return false;

            var otp = _otpService.GenerateOtp();

            var passwordHasher = new PasswordHasher<User>();
            var passwordHash = passwordHasher.HashPassword(null!, request.Password);

            // تخزين مؤقت
            _tempUsers[email] = new TempUser
            {
                Email = email,
                PasswordHash = passwordHash,
                RoleId = request.RoleId,
                OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
                OtpExpiry = DateTime.UtcNow.AddMinutes(10)
            };

            await _emailService.SendOtpAsync(email, otp);

            return true;
        }

        // ===== VERIFY OTP =====
        public async Task<string?> VerifyOtpAsync(string email, string otp)
        {
            email = email.ToLower().Trim();

            if (!_tempUsers.ContainsKey(email))
                return null;

            var temp = _tempUsers[email];

            if (temp.OtpExpiry < DateTime.UtcNow || !BCrypt.Net.BCrypt.Verify(otp, temp.OtpHash))
            {
                _tempUsers.Remove(email);
                return null;
            }

            var user = new User
            {
                Email = temp.Email,
                PasswordHash = temp.PasswordHash,
                RoleId = 2,
                IsEmailVerified = true
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            await _context.Entry(user).Reference(u => u.Role).LoadAsync();

            _tempUsers.Remove(email);

            return CreateToken(user);
        }

        // ===== LOGIN =====
        public async Task<string?> LoginAsync(UserDto request)
        {
            var email = request.Email.ToLower().Trim();

            var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

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

        // ===== TEMP USER =====
        private class TempUser
        {
            public string Email { get; set; } = null!;
            public string PasswordHash { get; set; } = null!;
            public int RoleId { get; set; }
            public string OtpHash { get; set; } = null!;
            public DateTime OtpExpiry { get; set; }
        }
    }
}