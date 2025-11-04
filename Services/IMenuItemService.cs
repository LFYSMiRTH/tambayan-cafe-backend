using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IMenuItemService
    {
        Task<List<Product>> GetTopSellingMenuItemsAsync(int limit = 5);

        Task<List<Product>> GetAvailableMenuItemsAsync();
    }
}