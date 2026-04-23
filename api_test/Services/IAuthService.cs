using api_test.Entities;
using api_test.Models;
using static api_test.Services.AuthService;

namespace api_test.Services
{
    public interface IAuthService
    {
        // Register: generates OTP, stores temp user, returns pendingToken (or null on failure)
        Task<RegisterInitResult?> RegisterAsync(UserDto request);

        // VerifyOtp: verifies OTP using pendingToken, creates user, returns JWT
        Task<string?> VerifyOtpAsync(string pendingToken, string otp);

        // Login: validates credentials directly, returns JWT immediately — NO OTP
        Task<string?> LoginAsync(UserDto request);

        // Get user by email
        Task<User?> GetUserByEmailAsync(string email);
    }
}