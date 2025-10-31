using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IInventoryService
    {
        Task<List<InventoryItem>> GetAllAsync();
    }
}