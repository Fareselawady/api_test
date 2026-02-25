using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IAuthService
    {
       
            // Register: بس يولد OTP ويرجع نجاح/فشل
            Task<bool> RegisterAsync(UserDto request);

            // VerifyOtp: بعد ما يدخل OTP صح، يتم تسجيله رسميًا ويرجع JWT
            Task<string?> VerifyOtpAsync(string email, string otp);

            // Login: زي القديم
            Task<string?> LoginAsync(UserDto request);

            // Get user by email: زي القديم
            Task<User?> GetUserByEmailAsync(string email);
        }
    
}
