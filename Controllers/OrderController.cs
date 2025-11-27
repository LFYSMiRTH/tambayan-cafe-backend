using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System;
using MongoDB.Bson;
using System.IO;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly ICustomerService _customerService;
        private readonly IProductService _productService;
        private readonly ILogger<OrderController> _logger;
        private readonly IDeliveryFeeService _deliveryFeeService;

        public OrderController(IOrderService orderService, InventoryService inventoryService, ICustomerService customerService, IProductService productService, ILogger<OrderController> logger, IDeliveryFeeService deliveryFeeService)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _customerService = customerService;
            _productService = productService;
            _logger = logger;
            _deliveryFeeService = deliveryFeeService;
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDto orderRequest)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            _logger.LogInformation("Raw order request body: {Body}", body);

            Request.Body.Position = 0;

            _logger.LogInformation("Received order payload: CustomerId={CustomerId}, CustomerEmail={CustomerEmail}, PlacedByStaff={PlacedByStaff}",
                orderRequest?.CustomerId,
                orderRequest?.CustomerEmail,
                orderRequest?.PlacedByStaff);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Model validation failed: {Errors}", string.Join(", ", errors));
                return BadRequest(ModelState);
            }
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                foreach (var item in orderRequest.Items)
                {
                    var product = await _productService.GetByIdAsync(item.ProductId);
                    if (product == null)
                    {
                        return BadRequest($"Product with ID {item.ProductId} not found.");
                    }

                    if (!product.IsAvailable)
                    {
                        return BadRequest($"Product '{product.Name}' is currently unavailable.");
                    }

                    if (item.Quantity > product.StockQuantity)
                    {
                        return BadRequest($"Insufficient stock for '{product.Name}'. Only {product.StockQuantity} available, but {item.Quantity} requested.");
                    }
                }

                foreach (var item in orderRequest.Items)
                {
                    var success = await _productService.TryDeductStockAsync(item.ProductId, item.Quantity);
                    if (!success)
                    {
                        return StatusCode(500, "Failed to deduct stock. Please try again.");
                    }
                }

                decimal deliveryFee = 0.0m;
                string deliveryAddress = string.Empty;

                bool isWalkIn = string.IsNullOrWhiteSpace(orderRequest.CustomerId) || orderRequest.CustomerId == "000000000000000000000000";
                if (!isWalkIn && !string.IsNullOrWhiteSpace(orderRequest.DeliveryAddress))
                {
                    deliveryAddress = orderRequest.DeliveryAddress.Trim();
                    deliveryFee = await _deliveryFeeService.CalculateDeliveryFeeAsync(deliveryAddress);
                }

                string customerName = "Walk-in Customer";
                string customerId = orderRequest.CustomerId ?? "";

                if (!string.IsNullOrWhiteSpace(customerId) && customerId != "000000000000000000000000")
                {
                    if (ObjectId.TryParse(customerId, out var objectId))
                    {
                        try
                        {
                            var customer = await _customerService.GetByIdAsync(customerId);
                            if (customer != null)
                            {
                                if (!string.IsNullOrWhiteSpace(customer.Name))
                                {
                                    customerName = customer.Name.Trim();
                                }
                                else if (!string.IsNullOrWhiteSpace(customer.FirstName) || !string.IsNullOrWhiteSpace(customer.LastName))
                                {
                                    customerName = $"{(customer.FirstName ?? "").Trim()} {(customer.LastName ?? "").Trim()}".Trim();
                                    if (string.IsNullOrEmpty(customerName))
                                    {
                                        customerName = customer.Username;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(customer.Username))
                                {
                                    customerName = customer.Username;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not fetch customer name for ID {CustomerId}", customerId);
                        }
                    }
                }

                var order = new Order
                {
                    CustomerId = string.IsNullOrWhiteSpace(orderRequest.CustomerId)
                        ? "000000000000000000000000"
                        : orderRequest.CustomerId,
                    CustomerEmail = string.IsNullOrWhiteSpace(orderRequest.CustomerEmail)
                        ? "walkin@tambayancafe.com"
                        : orderRequest.CustomerEmail,
                    CustomerName = customerName,
                    TableNumber = orderRequest.TableNumber ?? "N/A",
                    DeliveryAddress = deliveryAddress,
                    DeliveryFee = deliveryFee,
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
                    TotalAmount = orderRequest.TotalAmount + deliveryFee,
                    PlacedByStaff = orderRequest.PlacedByStaff,
                    UserId = orderRequest.PlacedByStaff ? (orderRequest.StaffId ?? "") : (orderRequest.CustomerId ?? ""),
                    Status = "New",
                    CreatedAt = DateTime.UtcNow,
                    OrderNumber = GenerateOrderNumber(),

                    PaymentMethod = string.IsNullOrWhiteSpace(orderRequest.PaymentMethod)
                        ? "Cash"
                        : orderRequest.PaymentMethod
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

        [HttpGet("orders/{id}")]
        public async Task<IActionResult> GetOrderById(string id)
        {
            try
            {
                if (!ObjectId.TryParse(id, out _))
                {
                    return BadRequest("Invalid order ID format.");
                }

                var order = await _orderService.GetOrderByIdAsync(id);
                if (order == null)
                {
                    return NotFound($"Order with ID {id} not found.");
                }

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order with ID {OrderId}", id);
                return StatusCode(500, new { message = "An error occurred while retrieving the order." });
            }
        }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; }
    }
}