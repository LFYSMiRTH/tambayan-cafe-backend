using System.Collections.Generic;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public interface ISupplierService
    {
        Task<List<Supplier>> GetAllAsync();
        Task<Supplier> GetByIdAsync(string id);
        Task<Supplier> AddAsync(Supplier supplier);
        Task<Supplier> UpdateAsync(string id, Supplier supplier);
        Task<bool> DeleteAsync(string id);
    }
}