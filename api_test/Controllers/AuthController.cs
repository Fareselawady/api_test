using api_test.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_test.Data;
using Microsoft.AspNetCore.Authorization;
using api_test.Models;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _context;

        public AuthController(IAuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        // ===== REGISTER =====
        [HttpPost("register")]
        public async Task<ActionResult> Register(UserDto request)
        {
            var result = await _authService.RegisterAsync(request);

            if (result == null)
                return BadRequest(new { message = "Email is already registered" });

            return Ok(new
            {
                message = "OTP sent to your email, please verify",
                pendingToken = result.PendingToken
            });
        }

        // ===== LOGIN =====
        [HttpPost("login")]
        public async Task<ActionResult> Login(UserDto request)
        {
            var token = await _authService.LoginAsync(request);

            if (token == null)
                return Unauthorized(new { message = "Invalid email or password" });

            var user = await _authService.GetUserByEmailAsync(request.Email);

            if (user == null)
                return NotFound(new { message = "User not found" });

            var userMeds = await _context.UserMedications
                .Include(um => um.Medication)
                .Where(um => um.UserId == user.Id)
                .ToListAsync();

            var userMedsResult = userMeds.Select(um => new
            {
                um.Id,
                um.MedicationId,
                MedName = UserMedicationFeatureHelper.GetDisplayName(um),
                MedicationName = UserMedicationFeatureHelper.GetDisplayName(um),
                um.IsCustomMedication,
                SupportsInteractions = UserMedicationFeatureHelper.SupportsInteractions(um),
                SupportsIngredientWarnings = UserMedicationFeatureHelper.SupportsIngredientWarnings(um),
                CustomMedicationWarning = UserMedicationFeatureHelper.GetCustomMedicationWarning(um),
                um.Dosage,
                um.Notes,
                StartDate = um.StartDate.HasValue ? um.StartDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                EndDate = um.EndDate.HasValue ? um.EndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                ExpiryDate = um.ExpiryDate.HasValue ? um.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
                um.IsOpened,
                um.OpenedDate,
                um.AfterOpeningDurationValue,
                um.AfterOpeningDurationUnit,
                um.AfterOpeningExpiryDate,
                EffectiveExpiryDate = MedicationExpiryHelper.GetEffectiveExpiryDate(um),
                um.ExpiryReason,
                um.AfterOpeningSource,
                AfterOpeningWarning = MedicationExpiryHelper.GetWarning(um),
                um.CurrentPillCount,
                um.InitialPillCount,
                um.LowStockThreshold,
                DosageForm = UserMedicationFeatureHelper.GetDosageForm(um),
                QuantityUnit = UserMedicationFeatureHelper.GetQuantityUnit(um),
                InitialQuantity = MedicationQuantityHelper.ResolveQuantity(um.InitialQuantity, um.InitialPillCount),
                CurrentQuantity = MedicationQuantityHelper.ResolveQuantity(um.CurrentQuantity, um.CurrentPillCount),
                DoseQuantity = MedicationQuantityHelper.ResolveQuantity(um.DoseQuantity, um.PillsPerDose),
                um.DosesPerPeriod,
                um.PeriodUnit,
                um.PeriodValue,
                FirstDoseTime = um.FirstDoseTime.HasValue ? um.FirstDoseTime.Value.ToTimeSpan() : (TimeSpan?)null,
                um.IntervalHours,
                um.NotificationActive
            }).ToList();

            return Ok(new
            {
                message = $"Welcome {user.Email}",
                token,
                user = new { user.Id, user.Email },
                myDrugs = userMedsResult
            });
        }

        // ===== VERIFY OTP (Registration flow) =====
        [HttpPost("verify-register-otp")]
        public async Task<IActionResult> VerifyRegisterOtp(VerifyOtpDto dto)
        {
            var token = await _authService.VerifyOtpAsync(dto.PendingToken, dto.Otp);

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Invalid or expired OTP" });

            return Ok(new { message = "Email verified", token });
        }

        // ===== AUTHENTICATED ENDPOINTS =====
        [Authorize]
        [HttpGet("me")]
        public IActionResult AuthenticatedUser()
        {
            return Ok(new { message = "You are authenticated" });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminOnly()
        {
            return Ok(new { message = "Welcome, Admin!" });
        }
    }
}
