using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IMongoCollection<Customer> _customers;

        public CustomerService(IMongoDatabase database)
        {
            _customers = database.GetCollection<Customer>("users");
        }

        public async Task<Customer> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var filter = Builders<Customer>.Filter.Eq("_id", id);
            return await _customers.Find(filter).FirstOrDefaultAsync();
        }
    }
}