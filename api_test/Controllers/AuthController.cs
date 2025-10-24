using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _context;

        public AuthController( AppDbContext _context, IAuthService authService)
        {
            this._context = _context;
            this._authService = authService;

        }

        [HttpPost("register")]
        public async Task<ActionResult<User>> Register(UserDto request)
        {
            var user = await _authService.RegisterAsync(request);
            if (user == null)
                return BadRequest("Username is already taken");

            return Ok(user);
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login(UserDto request)
        {
            var user = await _authService.LoginAsync(request);
            if (user == null)
                return BadRequest("Invalid username or password");

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);

            var drugs = await _context.Drugs
                .Where(d => d.UserId == user.Id)
                .Select(d => new DrugDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    Type = d.Type,
                    ExpirationDate = d.ExpirationDate,
                    ProductDate = d.ProductDate
                })
                .ToListAsync();

            return Ok(new
            {
                message = $"Welcome {user.Username}, you are now logged in.",
                myDrugs = drugs
            });
        }


        [HttpPost("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok("Logged out successfully.");
        }

        [HttpGet("session-info")]
        public IActionResult GetSessionInfo()
        {
            var username = HttpContext.Session.GetString("Username");
            var id = HttpContext.Session.GetInt32("UserId");

            if (username == null)
                return Unauthorized("No active session.");

            return Ok(new { id, username });
        }
    }
}
