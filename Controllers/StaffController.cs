using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/staff")]
    [Authorize(Roles = "staff")]
    public class StaffController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOrderService _orderService;
        private readonly IInventoryService _inventoryService;
        private readonly ILogger<StaffController> _logger;

        public StaffController(
            IUserService userService,
            IOrderService orderService,
            IInventoryService inventoryService,
            ILogger<StaffController> logger)
        {
            _userService = userService;
            _orderService = orderService;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        private IActionResult ValidateStaffRole()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "staff")
            {
                _logger.LogWarning("Access attempt to StaffController by non-staff user with role: {Role}", role);
                return Unauthorized(new { message = "Access denied. Staff role required." });
            }
            return null;
        }

        // GET api/staff/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardStats()
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                // Call a service method to fetch dashboard statistics for staff
                // You need to implement GetStaffDashboardStatsAsync in your service
                var stats = await _orderService.GetStaffDashboardStatsAsync(); // Example service call
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats for staff ID {StaffId}", User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while retrieving dashboard stats." });
            }
        }

        // GET api/staff/orders?limit=3&status=New,Preparing,Ready
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] int limit = 10, [FromQuery] string status = null)
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                var orders = await _orderService.GetOrdersForStaffAsync(limit, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders for staff ID {StaffId}. Query Params - Status: {Status}, Limit: {Limit}", User.FindFirst("id")?.Value, status, limit);
                return StatusCode(500, new { message = "An error occurred while retrieving orders." });
            }
        }

        // GET api/staff/inventory
        [HttpGet("inventory")]
        public async Task<IActionResult> GetInventory()
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                var inventory = await _inventoryService.GetAllInventoryItemsAsync();
                return Ok(inventory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory for staff ID {StaffId}", User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while retrieving inventory." });
            }
        }

        // GET api/staff/inventory/low-stock
        [HttpGet("inventory/low-stock")]
        public async Task<IActionResult> GetLowStockItems()
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                // Call a service method to fetch low stock items
                // You need to implement GetLowStockItemsAsync in your service
                var lowStockItems = await _inventoryService.GetLowStockItemsAsync();
                return Ok(lowStockItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching low stock items for staff ID {StaffId}", User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while retrieving low stock items." });
            }
        }

        // PUT api/staff/orders/{orderId}/status
        [HttpPut("orders/{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string orderId, [FromBody] OrderStatusUpdateDto updateDto)
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            // Basic validation
            if (string.IsNullOrEmpty(updateDto?.Status))
            {
                return BadRequest(new { message = "Status is required." });
            }

            try
            {
                // Call a service method to update the order status
                // You need to implement UpdateOrderStatusAsync in your service
                var updatedOrder = await _orderService.UpdateOrderStatusAsync(orderId, updateDto.Status);

                if (updatedOrder == null)
                {
                    // Log if the order wasn't found
                    _logger.LogWarning("Order ID {OrderId} not found for status update by staff ID {StaffId}", orderId, User.FindFirst("id")?.Value);
                    return NotFound(new { message = "Order not found." });
                }

                return Ok(updatedOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status for Order ID {OrderId} by staff ID {StaffId}", orderId, User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while updating order status." });
            }
        }

        // POST api/staff/orders/{orderId}/print
        [HttpPost("orders/{orderId}/print")]
        public async Task<IActionResult> PrintReceipt(string orderId)
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                // Implement your print logic here
                // This could involve calling a printer service, generating a PDF, etc.
                // For now, just return success as a placeholder
                // You might want to validate if the order ID exists first
                var orderExists = await _orderService.GetOrderByIdAsync(orderId); // Example service call
                if (orderExists == null)
                {
                    _logger.LogWarning("Attempt to print receipt for non-existent Order ID {OrderId} by staff ID {StaffId}", orderId, User.FindFirst("id")?.Value);
                    return NotFound(new { message = "Order not found." });
                }

                // Simulate print success
                // var printResult = await _printerService.PrintOrderReceiptAsync(orderId); // Example call
                _logger.LogInformation("Receipt for Order ID {OrderId} sent to printer by staff ID {StaffId}", orderId, User.FindFirst("id")?.Value);

                return Ok(new { message = "Receipt for order " + orderId + " sent to printer." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing receipt for Order ID {OrderId} by staff ID {StaffId}", orderId, User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while printing the receipt." });
            }
        }

        // POST api/staff/orders/ready/print-all
        [HttpPost("orders/ready/print-all")]
        public async Task<IActionResult> PrintAllReadyOrders()
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {

                _logger.LogInformation("Print all ready orders request received by staff ID {StaffId}", User.FindFirst("id")?.Value);

                return Ok(new { message = "All ready orders sent to printer." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing all ready orders by staff ID {StaffId}", User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while printing ready orders." });
            }
        }

        // POST api/staff/inventory/alert
        [HttpPost("inventory/alert")]
        public async Task<IActionResult> SendLowStockAlert([FromBody] LowStockAlertDto alertDto)
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            // Basic validation - ✅ IMPROVED CHECK
            if (alertDto == null || string.IsNullOrEmpty(alertDto.ItemName))
            {
                return BadRequest(new { message = "Item name is required." });
            }

            try
            {
                _logger.LogWarning("Low stock alert sent for item '{ItemName}' by staff ID {StaffId}", alertDto.ItemName, User.FindFirst("id")?.Value);

                // ✅ UNCOMMENTED AND ADDED SERVICE CALL
                await _inventoryService.SendLowStockAlertAsync(alertDto.ItemName);

                return Ok(new { message = "Low stock alert for '" + alertDto.ItemName + "' sent successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending low stock alert for item '{ItemName}' by staff ID {StaffId}", alertDto.ItemName, User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while sending the low stock alert." });
            }
        }

        // GET api/staff/notifications?limit=5
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications([FromQuery] int limit = 5)
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            try
            {
                var notifications = new[]
                {
                    new { message = "Order #201 is now ready for pickup!", createdAt = DateTime.UtcNow.AddMinutes(-2) },
                    new { message = "New customer feedback received.", createdAt = DateTime.UtcNow.AddHours(-1) },
                    new { message = "Inventory item 'Coffee Beans' is running low.", createdAt = DateTime.UtcNow.AddHours(-3) }
                }.Take(limit).ToArray();

                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notifications for staff ID {StaffId}", User.FindFirst("id")?.Value);
                return StatusCode(500, new { message = "An error occurred while retrieving notifications." });
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (ValidateStaffRole() is IActionResult unauthorizedResult)
                return unauthorizedResult;

            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var staff = await _userService.GetUserByIdAsync(userId);
            if (staff == null || staff.Role.ToLower() != "staff")
                return NotFound();

            return Ok(new
            {
                staff.Id,
                staff.Username, // Or staff.Name if you have that property
                staff.Email,
                staff.Role
            });
        }
    }

    // DTOs for request bodies
    public class OrderStatusUpdateDto
    {
        public string Status { get; set; }
    }

    public class LowStockAlertDto
    {
        public string ItemName { get; set; }
    }
}