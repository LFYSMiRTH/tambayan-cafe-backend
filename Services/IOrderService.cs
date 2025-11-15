using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IOrderService
    {
        Task<List<Order>> GetOrdersByCustomerIdAsync(string customerId, int limit = 3, string status = null);
        Task<Order> CreateOrderAsync(OrderRequestDto orderRequest); 

        Task<object> GetStaffDashboardStatsAsync(); 
        Task<IEnumerable<Order>> GetOrdersForStaffAsync(int limit, string statusFilter); 
        Task<Order> UpdateOrderStatusAsync(string orderId, string newStatus);
        Task<Order> GetOrderByIdAsync(string orderId);
        
    }
}