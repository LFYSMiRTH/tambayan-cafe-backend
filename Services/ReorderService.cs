// Services/ReorderService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class ReorderService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;
        private readonly ILogger<ReorderService> _logger;

        public ReorderService(IMongoDatabase database, ILogger<ReorderService> logger)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
            _logger = logger;
        }

        /// <summary>
        /// Checks for low-stock items with auto-reorder enabled and replenishes by 10 pcs.
        /// </summary>
        public async Task CheckAndReorderAsync()
        {
            var filter = Builders<InventoryItem>.Filter.Where(i =>
                i.CurrentStock <= i.ReorderLevel &&
                i.IsAutoReorderEnabled == true);

            var lowStockItems = await _inventory.Find(filter).ToListAsync();

            foreach (var item in lowStockItems)
            {
                // 🔥 Fixed: 10 pcs auto-replenishment (no LastReorderedAt)
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
                        "✅ Auto-reordered: {Name} — +{Qty} {Unit} (ReorderLevel: {ReorderLevel}, New Stock: {NewStock})",
                        item.Name, reorderAmount, item.Unit, item.ReorderLevel, newStock);
                }
            }
        }
    }
}