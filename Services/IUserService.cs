using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(string id);
    }
}