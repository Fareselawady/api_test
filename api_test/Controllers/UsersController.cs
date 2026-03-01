using api_test.Data;
using api_test.Entities;
using api_test.Models;
using api_test.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace api_test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> AddUserByAdmin([FromBody] CreateUserDto newUserDto)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Email == newUserDto.Email);
            if (userExists)
                return BadRequest(new { message = "Email already registered" });

            var passwordHasher = new PasswordHasher<User>();

            var user = new User
            {
                Email = newUserDto.Email,
                PasswordHash = passwordHasher.HashPassword(null!, newUserDto.Password),
                RoleId = newUserDto.RoleId,
                IsEmailVerified = true,
                Name = newUserDto.Name ?? string.Empty,
                Username = newUserDto.Username ?? string.Empty,
                Phone = newUserDto.Phone ?? string.Empty,
                BirthDate = newUserDto.BirthDate,
                Gender = newUserDto.Gender ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User added by Admin successfully", userId = user.Id });
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });

            var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var isAdmin = User.IsInRole("Admin");

            // منع المستخدم العادي من تعديل بيانات غيره
            if (!isAdmin && currentUserId != id)
                return Forbid();

            // تعديل الحقول الشخصية (لكل المستخدمين)
            if (dto.Name != null) user.Name = dto.Name;
            if (dto.Username != null) user.Username = dto.Username;
            if (dto.Phone != null) user.Phone = dto.Phone;
            if (dto.BirthDate.HasValue) user.BirthDate = dto.BirthDate;
            if (dto.Gender != null) user.Gender = dto.Gender;

            // تعديل الباسورد
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                var passwordHasher = new PasswordHasher<User>();
                user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);
            }

            // تعديل الصلاحيات (Admin فقط)
            if (isAdmin && dto.RoleId.HasValue)
                user.RoleId = dto.RoleId.Value;

            await _context.SaveChangesAsync();

            return Ok(new { message = "User updated successfully" });
        }
        [Authorize(Roles = "Admin")]
        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role) // جلب الدور لكل مستخدم
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Username,
                    u.Email,
                    u.Phone,
                    u.BirthDate,
                    u.Gender,
                    u.CreatedAt,
                    Role = new { u.Role.RoleId, u.Role.RoleName }
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            var user = await _context.Users
                .Include(u => u.UserMedications)  // جلب الأدوية
                    .ThenInclude(um => um.MedicationSchedules) // جلب الجدولة
                .Include(u => u.Alerts) // جلب الإشعارات
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { message = "User not found" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User {user.Email} and all related data deleted successfully" });
        }

    }
}
