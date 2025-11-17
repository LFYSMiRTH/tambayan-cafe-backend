using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization; 

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

        // ✅ ADD: POST endpoint for staff to send low stock alerts
        // Add the [Authorize(Roles = "staff")] attribute here
        [HttpPost("staff/inventory/alert")]
        [Authorize(Roles = "staff")] // ✅ Ensure only staff can call this
        public async Task<IActionResult> SendLowStockAlert([FromBody] SendLowStockAlertDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.ItemName)) // Add null check for dto itself
                {
                    return BadRequest("Item name is required.");
                }

                // ✅ Call the service to handle the business logic and notification creation
                await _inventoryService.SendLowStockAlertAsync(dto.ItemName);

                return Ok(new { message = "Low stock alert sent successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending low stock alert for item {ItemName}", dto?.ItemName); // Log potential null ItemName safely
                return StatusCode(500, new { message = "An error occurred while sending the alert." });
            }
        }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; }
    }

    // ✅ ADD: DTO for the alert request body
    public class SendLowStockAlertDto
    {
        public string ItemName { get; set; }
    }
}