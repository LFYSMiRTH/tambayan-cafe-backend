using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IOrderService
    {
        Task<List<Order>> GetOrdersByCustomerIdAsync(string customerId, int limit = 3, string status = null);
    }
}