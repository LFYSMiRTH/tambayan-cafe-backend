using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson;

namespace TambayanCafeAPI.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;

        public InventoryService(IMongoDatabase database)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
        }

        public List<InventoryItem> GetAll() =>
            _inventory.Find(_ => true).ToList();

        public async Task<List<InventoryItem>> GetAllAsync() =>
            await _inventory.Find(_ => true).ToListAsync();

        public async Task<IEnumerable<InventoryItem>> GetAllInventoryItemsAsync() =>
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

        public InventoryItem GetById(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null;

            var filter = Builders<InventoryItem>.Filter.Eq("_id", objectId);
            return _inventory.Find(filter).FirstOrDefault();
        }

        public async Task<IEnumerable<InventoryItem>> GetLowStockItemsAsync()
        {
            int lowStockThreshold = 5;
            var filter = Builders<InventoryItem>.Filter.Lt(ii => ii.CurrentStock, lowStockThreshold);

            var lowStockItems = await _inventory.Find(filter).ToListAsync();
            return lowStockItems;
        }
    }
}