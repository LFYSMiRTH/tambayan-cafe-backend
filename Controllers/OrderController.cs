using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Models; // Ensure this matches your models namespace
using TambayanCafeAPI.Services; // Ensure this matches your services namespace
using Microsoft.Extensions.Logging; // For logging

namespace TambayanCafeSystem.Controllers // Ensure this matches your controllers namespace
{
    [ApiController]
    [Route("api/[controller]")] // This sets the base route for the controller
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger; // Inject logger

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        // POST: api/orders
        [HttpPost("orders")] // This appends 'orders' to the base route, making it '/api/orders'
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequestDto orderRequest)
        {
            try
            {
                // Validate the incoming model state
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Call your service to handle the business logic and database interaction
                var createdOrder = await _orderService.CreateOrderAsync(orderRequest);

                // Return success response (200 OK) with the created order details
                return Ok(createdOrder);
            }
            catch (System.Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error creating order for customer {CustomerId}", orderRequest?.CustomerId);

                // Return a generic error to the client (500 Internal Server Error)
                return StatusCode(500, new { message = "An error occurred while processing your order." });
            }
        }
    }
}