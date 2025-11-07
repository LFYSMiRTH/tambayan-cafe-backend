using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson; 

namespace TambayanCafeAPI.Services
{
    public class InventoryService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;

        public InventoryService(IMongoDatabase database)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory"); // Ensure collection name is correct
        }

        public List<InventoryItem> GetAll() =>
            _inventory.Find(_ => true).ToList();

        public async Task<List<InventoryItem>> GetAllInventoryItemsAsync() =>
            await _inventory.Find(_ => true).ToListAsync();

        public void Create(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _inventory.InsertOne(item);
        }

        public async Task CreateAsync(InventoryItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            await _inventory.InsertOneAsync(item);
        }

        public IMongoCollection<InventoryItem> GetCollection() => _inventory;

        // Add this new method
        public InventoryItem GetById(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null; // Or throw an exception if preferred

            var filter = Builders<InventoryItem>.Filter.Eq("_id", objectId);
            return _inventory.Find(filter).FirstOrDefault();
        }
    }
}