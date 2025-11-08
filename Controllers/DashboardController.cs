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

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var totalOrders = _orderService.GetTotalCount();
                var pendingOrders = _orderService.GetPendingCount();
                var totalRevenue = _orderService.GetTotalRevenue();

                var lowStockInventoryCount = await GetLowStockInventoryCountAsync();
                var lowStockProductCount = await GetLowStockProductCountAsync();
                var totalLowStockAlerts = lowStockInventoryCount + lowStockProductCount;
                var unreadNotifications = await _notificationService.GetUnreadCountAsync();

                return Ok(new
                {
                    totalOrders,
                    pendingOrders,
                    totalRevenue,
                    lowStockAlerts = totalLowStockAlerts,
                    unreadNotifications
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load dashboard stats", details = ex.Message });
            }
        }

        [HttpGet("notifications/unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            try
            {
                var persistentNotifications = await _notificationService.GetUnreadAsync();
                var lowStockNotifications = new List<Notification>();

                // 🔥 FIXED: Use expression, not lambda-lambda (avoids type mismatch)
                var lowStockIngredients = await _inventoryService.GetCollection()
                    .Find(i => i.CurrentStock <= i.ReorderLevel) // ✅ Works if both are numeric
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

                // 🔥 FIXED: Same fix for products
                var lowStockProducts = await _productService.GetCollection()
                    .Find(p => p.StockQuantity <= p.LowStockThreshold && p.IsAvailable)
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

        private async Task<long> GetLowStockInventoryCountAsync()
        {
            // 🔥 Use LINQ-style filter to avoid type issues
            return await _inventoryService.GetCollection()
                .CountDocumentsAsync(i => i.CurrentStock <= i.ReorderLevel);
        }

        private async Task<long> GetLowStockProductCountAsync()
        {
            return await _productService.GetCollection()
                .CountDocumentsAsync(p => p.StockQuantity <= p.LowStockThreshold && p.IsAvailable);
        }
    }
}