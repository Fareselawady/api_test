using api_test.Entities;
using api_test.Models;

namespace api_test.Services
{
    public interface IAuthService
    {
        Task<User?> RegisterAsync(UserDto request);

        Task<string?> LoginAsync(UserDto request);

        Task<User?> GetUserByUsernameAsync(string username);
    }
}
