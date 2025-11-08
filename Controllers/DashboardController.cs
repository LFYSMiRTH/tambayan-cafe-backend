using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Services;
using TambayanCafeAPI.Models;
using System.Threading.Tasks;
using System;
using System.Linq;
using MongoDB.Driver;
using MongoDB.Bson;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly ProductService _productService;
        private readonly NotificationService _notificationService;

        public DashboardController(
            OrderService orderService,
            InventoryService inventoryService,
            ProductService productService,
            NotificationService notificationService)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _productService = productService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get key dashboard stats for admin homepage
        /// </summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var totalOrders = _orderService.GetTotalCount();
                var pendingOrders = _orderService.GetPendingCount();
                var totalRevenue = _orderService.GetTotalRevenue();

                var lowStockInventoryCount = await GetLowStockInventoryCountAsync();
                var lowStockProductCount = await GetLowStockProductCountAsync(); // ✅ Updated method

                var totalLowStockAlerts = lowStockInventoryCount + lowStockProductCount;

                var unreadNotifications = await _notificationService.GetUnreadCountAsync();

                return Ok(new
                {
                    totalOrders,
                    pendingOrders,
                    totalRevenue,
                    lowStockAlerts = totalLowStockAlerts, // ✅ Now includes both!
                    unreadNotifications
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load dashboard stats", details = ex.Message });
            }
        }

        /// <summary>
        /// Get list of unread notifications (including dynamic low-stock alerts)
        /// </summary>
        [HttpGet("notifications/unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            try
            {
                var persistentNotifications = await _notificationService.GetUnreadAsync();
                var lowStockNotifications = new System.Collections.Generic.List<Notification>();

                // a. Low-stock INGREDIENTS (currentStock <= reorderLevel)
                var lowStockIngredients = await _inventoryService.GetCollection()
                    .Find(Builders<InventoryItem>.Filter.Lte("currentStock", "reorderLevel"))
                    .Limit(10)
                    .ToListAsync();

                foreach (var item in lowStockIngredients)
                {
                    lowStockNotifications.Add(new Notification
                    {
                        Id = $"inv-{item.Id}-low",
                        Message = $"⚠️ Low Stock: Ingredient '{item.Name}' is at {item.CurrentStock} (reorder at {item.ReorderLevel})",
                        Type = "warning",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }

                // b. Low-stock PRODUCTS (stockQuantity <= lowStockThreshold AND isAvailable == true)
                var lowStockProducts = await _productService.GetCollection()
                    .Find(Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Lte("stockQuantity", "lowStockThreshold"),
                        Builders<Product>.Filter.Eq("isAvailable", true)
                    ))
                    .Limit(10)
                    .ToListAsync();

                foreach (var item in lowStockProducts)
                {
                    lowStockNotifications.Add(new Notification
                    {
                        Id = $"prod-{item.Id}-low",
                        Message = $"⚠️ Low Stock: Product '{item.Name}' has {item.StockQuantity} left (threshold: {item.LowStockThreshold})",
                        Type = "warning",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }

                var allNotifications = persistentNotifications
                    .Where(n => !n.Id.StartsWith("inv-") && !n.Id.StartsWith("prod-"))
                    .Concat(lowStockNotifications)
                    .OrderByDescending(n => n.CreatedAt)
                    .Take(20)
                    .ToList();

                return Ok(allNotifications);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load notifications", details = ex.Message });
            }
        }

        /// <summary>
        /// Mark a notification as read
        /// </summary>
        [HttpPost("notifications/{id}/read")]
        public async Task<IActionResult> MarkNotificationAsRead(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Notification ID is required.");

            try
            {
                await _notificationService.MarkAsReadAsync(id);
                return Ok(new { message = "Notification marked as read." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to mark notification as read.", details = ex.Message });
            }
        }

        /// <summary>
        /// Count inventory items where currentStock <= reorderLevel
        /// </summary>
        private async Task<long> GetLowStockInventoryCountAsync()
        {
            var filter = Builders<InventoryItem>.Filter.Lte("currentStock", "reorderLevel");
            return await _inventoryService.GetCollection().CountDocumentsAsync(filter);
        }

        /// <summary>
        /// Count products where stockQuantity <= lowStockThreshold AND isAvailable == true
        /// </summary>
        private async Task<long> GetLowStockProductCountAsync()
        {
            var filter = Builders<Product>.Filter.And(
                Builders<Product>.Filter.Lte("stockQuantity", "lowStockThreshold"),
                Builders<Product>.Filter.Eq("isAvailable", true)
            );
            return await _productService.GetCollection().CountDocumentsAsync(filter);
        }
    }
}