using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface IProductService
    {
        Task<Product> GetByIdAsync(string id);
        Task UpdateAsync(Product product);
        Task<List<Product>> GetAllAsync();
        Task<bool> TryDeductStockAsync(string productId, int quantity);
    }
}