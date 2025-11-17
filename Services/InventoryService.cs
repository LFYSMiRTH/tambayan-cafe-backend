using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson;
using Microsoft.Extensions.Logging; // Add logging

namespace TambayanCafeAPI.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;
        private readonly NotificationService _notificationService; // Inject NotificationService
        private readonly ILogger<InventoryService> _logger; // Inject logger

        public InventoryService(IMongoDatabase database, NotificationService notificationService, ILogger<InventoryService> logger) // Add NotificationService and ILogger to constructor
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
            _notificationService = notificationService; // Assign injected service
            _logger = logger; // Assign injected logger
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

        // ✅ ADD: Method to send low stock alert notification
        public async Task SendLowStockAlertAsync(string itemName)
        {
            var notification = new Notification
            {
                Message = $"⚠️ Low stock alert for '{itemName}'.",
                Type = "warning", // Use 'warning' type for alerts
                Category = "inventory", // Set a specific category for inventory alerts
                TargetRole = "admin", // ✅ Set the target role to "admin"
                CreatedAt = DateTime.UtcNow,
                IsRead = false // Ensure it's unread initially
            };

            await _notificationService.CreateAsync(notification); // Call the notification service
            _logger.LogInformation("Low stock alert sent for item: {ItemName}", itemName);
        }
    }
}