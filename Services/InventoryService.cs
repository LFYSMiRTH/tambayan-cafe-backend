using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class InventoryService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;

        public InventoryService(IMongoDatabase database)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
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
    }
}