using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Services;
using TambayanCafeAPI.Models;
using System.Threading.Tasks;
using System;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly NotificationService _notificationService;

        public DashboardController(
            OrderService orderService,
            InventoryService inventoryService,
            NotificationService notificationService)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
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
                var lowStockCount = await GetLowStockItemCountAsync();
                var unreadNotifications = await _notificationService.GetUnreadCountAsync();

                // ✅ FIXED: Renamed "lowStockCount" → "lowStockAlerts" to match frontend expectation
                return Ok(new
                {
                    totalOrders,
                    pendingOrders,
                    totalRevenue,
                    lowStockAlerts = lowStockCount, // 🔑 KEY FIX
                    unreadNotifications
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load dashboard stats", details = ex.Message });
            }
        }

        /// <summary>
        /// Get list of unread notifications (for bell icon or toast)
        /// </summary>
        [HttpGet("notifications/unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            try
            {
                var notifications = await _notificationService.GetUnreadAsync();
                return Ok(notifications);
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
        /// Helper: Count how many inventory items are below reorder level
        /// </summary>
        private async Task<long> GetLowStockItemCountAsync()
        {
            // 🔥 Use same logic as ReorderService, but just count
            var filter = new MongoDB.Driver.BsonDocumentFilterDefinition<InventoryItem>(
                new MongoDB.Bson.BsonDocument("$expr",
                    new MongoDB.Bson.BsonDocument("$lte",
                        new MongoDB.Bson.BsonArray { "$CurrentStock", "$ReorderLevel" })));

            return await _inventoryService.GetCollection().CountDocumentsAsync(filter);
        }
    }
}