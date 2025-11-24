using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, InventoryService inventoryService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto orderRequest)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var order = new Order
                {
                    CustomerId = string.IsNullOrWhiteSpace(orderRequest.CustomerId)
                        ? "000000000000000000000000"
                        : orderRequest.CustomerId,
                    CustomerEmail = string.IsNullOrWhiteSpace(orderRequest.CustomerEmail)
                        ? "walkin@tambayancafe.com"
                        : orderRequest.CustomerEmail,
                    CustomerName = string.IsNullOrWhiteSpace(orderRequest.CustomerName)
                        ? "Walk-in Customer"
                        : orderRequest.CustomerName,
                    TableNumber = orderRequest.TableNumber ?? "N/A",
                    Items = orderRequest.Items.Select(item => new OrderItem
                    {
                        ProductId = item.ProductId,
                        Name = item.Name,
                        Price = item.Price,
                        Quantity = item.Quantity,
                        Size = item.Size,
                        Mood = item.Mood,
                        Sugar = item.Sugar
                    }).ToList(),
                    TotalAmount = orderRequest.TotalAmount,
                    PlacedByStaff = orderRequest.PlacedByStaff, // Now valid
                    UserId = orderRequest.PlacedByStaff ? (orderRequest.StaffId ?? "") : (orderRequest.CustomerId ?? ""),
                    Status = "New",
                    CreatedAt = DateTime.UtcNow,
                    OrderNumber = GenerateOrderNumber()
                };

                var createdOrder = await _orderService.CreateOrderAsyncFromModel(order);
                return Ok(createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for customer {CustomerId}", orderRequest?.CustomerId);
                return StatusCode(500, new { message = "An error occurred while processing your order." });
            }
        }

        private string GenerateOrderNumber()
        {
            return $"ORD-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
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

        [HttpGet("orders/staff")]
        public async Task<IActionResult> GetStaffOrders([FromQuery] int limit = 100, [FromQuery] string status = null)
        {
            try
            {
                var orders = await _orderService.GetOrdersForStaffAsync(limit, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for staff");
                return StatusCode(500, new { message = "An error occurred while retrieving orders." });
            }
        }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; }
    }
}