using MongoDB.Driver;
using TambayanCafeAPI.Models;
// Add using directive for the namespace containing ISupplierService
using TambayanCafeAPI.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace TambayanCafeSystem.Services
{
    // Change 'public class SupplierService' to 'public class SupplierService : ISupplierService'
    // This tells the compiler that SupplierService implements the ISupplierService interface
    public class SupplierService : ISupplierService
    {
        private readonly IMongoCollection<Supplier> _suppliers;

        public SupplierService(IMongoDatabase database)
        {
            _suppliers = database.GetCollection<Supplier>("Suppliers");
        }

        // Implement the methods defined in ISupplierService
        public async Task<List<Supplier>> GetAllAsync()
        {
            // Implement the async version based on your existing sync version
            return await _suppliers.Find(_ => true).ToListAsync();
        }

        public async Task<Supplier> GetByIdAsync(string id)
        {
            // Assuming 'id' is a string representation of ObjectId
            if (!ObjectId.TryParse(id, out var objectId))
                return null; // Or throw an exception

            var filter = Builders<Supplier>.Filter.Eq("_id", objectId);
            return await _suppliers.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Supplier> AddAsync(Supplier supplier)
        {
            if (string.IsNullOrEmpty(supplier.Id))
            {
                supplier.Id = null; // Let MongoDB assign ObjectId
            }
            await _suppliers.InsertOneAsync(supplier);
            return supplier;
        }

        public async Task<Supplier> UpdateAsync(string id, Supplier supplier)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null; // Or handle error appropriately

            var filter = Builders<Supplier>.Filter.Eq("_id", objectId);
            await _suppliers.ReplaceOneAsync(filter, supplier);
            return supplier; // Or return a confirmation, or the updated object
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return false; // Or handle error appropriately

            var filter = Builders<Supplier>.Filter.Eq("_id", objectId);
            var result = await _suppliers.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        // Keep your existing synchronous methods if needed elsewhere
        public List<Supplier> GetAll()
        {
            return _suppliers.Find(_ => true).ToList();
        }

        public Supplier Create(Supplier supplier)
        {
            // MongoDB auto-generates ObjectId if Id is empty
            if (string.IsNullOrEmpty(supplier.Id))
            {
                supplier.Id = null; // Let MongoDB assign it
            }
            _suppliers.InsertOne(supplier);
            return supplier;
        }

        public void Update(string id, Supplier supplier)
        {
            var filter = Builders<Supplier>.Filter.Eq(s => s.Id, id);
            _suppliers.ReplaceOne(filter, supplier);
        }
    }
}