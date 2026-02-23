using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using api_test.Data;
using Microsoft.AspNetCore.Authorization;

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

        [HttpPost("register")]
        public async Task<ActionResult> Register(UserDto request)
        {
            var user = await _authService.RegisterAsync(request);

            if (user == null)
                return BadRequest(new { message = "Username is already taken" });

            return Ok(new
            {
                message = "Registration successful",
                user = new { user.Id, user.Username }
            });
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserDto request)
        {
            var token = await _authService.LoginAsync(request);

            if (token == null)
                return Unauthorized(new { message = "Invalid username or password" });

            var user = await _authService.GetUserByUsernameAsync(request.Username);

            if (user == null)
                return NotFound(new { message = "User not found" });

            var userMeds = await _context.UserMedications
          .Include(um => um.Medication)
          .Where(um => um.UserId == user.Id)
          .Select(um => new
          {
              um.Id,
              MedId = um.MedId,
              MedName = um.Medication.Trade_name,
              um.Dosage,
              um.Notes,
              um.StartDate,
              um.EndDate,
              um.ExpiryDate,
              um.CurrentPillCount,
              um.InitialPillCount,
              um.LowStockThreshold,
              um.DosesPerPeriod,
              um.PeriodUnit,
              um.PeriodValue,
              um.FirstDoseTime,
              um.IntervalHours,
              um.NotificationActive
          })
                  .ToListAsync();

            return Ok(new
            {
                message = $"Welcome {user.Username}",
                token,
                user = new { user.Id, user.Username },
                myDrugs = userMeds
            });
        }
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
