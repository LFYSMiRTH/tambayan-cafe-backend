using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface ICustomerService
    {
        Task<Customer> GetByIdAsync(string id);
    }
}