using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(string id);
        Task<User> GetUserProfileAsync(string userId);
        Task<bool> UpdateUserProfileAsync(string userId, User updatedUser);
        Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<bool> DeleteAccountAsync(string userId, string passwordConfirmation);
    }
}