using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDB.Bson;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class ReorderService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;
        private readonly ILogger<ReorderService> _logger;
        private readonly NotificationService _notificationService;

        public ReorderService(
            IMongoDatabase database,
            ILogger<ReorderService> logger,
            NotificationService notificationService)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task CheckAndReorderAsync()
        {
            // ✅ PascalCase field names + BsonDocument $expr (v2.7+ compatible)
            var filter = new BsonDocumentFilterDefinition<InventoryItem>(
                new BsonDocument("$expr",
                    new BsonDocument("$lte",
                        new BsonArray { "$CurrentStock", "$ReorderLevel" })));

            var lowStockItems = await _inventory.Find(filter).ToListAsync();

            if (lowStockItems.Count == 0)
            {
                _logger.LogDebug("🔍 [2m] No items below reorder level.");
                return;
            }

            foreach (var item in lowStockItems)
            {
                const int reorderAmount = 10;

                var update = Builders<InventoryItem>.Update
                    .Inc(i => i.CurrentStock, reorderAmount);

                var result = await _inventory.UpdateOneAsync(
                    Builders<InventoryItem>.Filter.Eq("_id", ObjectId.Parse(item.Id)),
                    update);

                if (result.ModifiedCount > 0)
                {
                    var newStock = item.CurrentStock + reorderAmount;
                    _logger.LogInformation(
                        "✅ AUTO-REORDERED: {Name} — +{Qty} {Unit} (ReorderLevel: {ReorderLevel}, New Stock: {NewStock})",
                        item.Name, reorderAmount, item.Unit, item.ReorderLevel, newStock);

                    // 🔔 NEW: Create notification
                    var notification = new Notification
                    {
                        Message = $"⚠️ Auto-reordered {item.Name}: +{reorderAmount} {item.Unit} (was {item.CurrentStock}, now {newStock})",
                        Type = "warning",
                        Category = "stock",
                        RelatedId = item.Id,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _notificationService.CreateAsync(notification);
                }
            }
        }
    }
}