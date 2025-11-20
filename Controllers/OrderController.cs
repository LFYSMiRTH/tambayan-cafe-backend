using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly InventoryService _inventoryService; // ✅ Inject InventoryService
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, InventoryService inventoryService, ILogger<OrderController> logger) // ✅ Add InventoryService to constructor
        {
            _orderService = orderService;
            _inventoryService = inventoryService; // ✅ Assign injected service
            _logger = logger;
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequestDto orderRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var createdOrder = await _orderService.CreateOrderAsync(orderRequest);
                return Ok(createdOrder);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error creating order for customer {CustomerId}", orderRequest?.CustomerId);
                return StatusCode(500, new { message = "An error occurred while processing your order." });
            }
        }

        [HttpPut("orders/{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string orderId, [FromBody] UpdateOrderStatusDto statusDto)
        {
            try
            {
                if (statusDto == null || string.IsNullOrEmpty(statusDto.Status))
                {
                    return BadRequest("Status is required.");
                }

                var validStatuses = new[] { "New", "Preparing", "Ready", "Completed", "Served", "Pending", "Cancelled" };
                if (!validStatuses.Contains(statusDto.Status))
                {
                    return BadRequest($"Invalid status: {statusDto.Status}");
                }

                var updatedOrder = await _orderService.UpdateOrderStatusAsync(orderId, statusDto.Status);

                if (updatedOrder == null)
                {
                    return NotFound();
                }

                return Ok(new { message = "Order status updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status for order {OrderId}", orderId);
                return StatusCode(500, new { message = "An error occurred while updating the order status." });
            }
        }

        // Renamed method to avoid conflict with StaffController
        [HttpGet("orders/staff")]
        public async Task<IActionResult> GetStaffOrders([FromQuery] int limit = 100, [FromQuery] string status = null) // Changed parameter name from statusFilter to status
        {
            try
            {
                // Pass the 'status' parameter to the service method
                var orders = await _orderService.GetOrdersForStaffAsync(limit, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for staff");
                return StatusCode(500, new { message = "An error occurred while retrieving orders." });
            }
        }

        // ✅ ADD THIS NEW ENDPOINT FOR CUSTOMER ORDERS
        [HttpGet("customer/orders")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> GetCustomerOrders([FromQuery] string status = null, [FromQuery] int limit = 100)
        {
            try
            {
                var customerId = User.FindFirst("id")?.Value; // Get customer ID from JWT token
                if (string.IsNullOrEmpty(customerId))
                {
                    return Unauthorized("Customer ID not found in token.");
                }

                var orders = await _orderService.GetOrdersByCustomerIdAsync(customerId, limit, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for customer");
                return StatusCode(500, new { message = "An error occurred while retrieving your orders." });
            }
        }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; }
    }
}