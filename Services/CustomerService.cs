using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IMongoCollection<User> _users;

        public CustomerService(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("users");
        }

        public async Task<Customer> GetByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out var objectId))
                return null;

            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var projection = Builders<User>.Projection
                .Include(u => u.Id)
                .Include(u => u.Name)
                .Include(u => u.FirstName)
                .Include(u => u.LastName)
                .Include(u => u.Username)
                .Include(u => u.Email);

            var user = await _users.Find(filter).Project<User>(projection).FirstOrDefaultAsync();
            if (user == null)
                return null;

            return new Customer
            {
                Id = user.Id,
                Name = user.Name,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                Email = user.Email
            };
        }
    }
}