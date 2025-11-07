using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/customer")]
    [Authorize(Roles = "customer")]
    public class CustomerController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOrderService _orderService;
        private readonly IMenuItemService _menuItemService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(
            IUserService userService,
            IOrderService orderService,
            IMenuItemService menuItemService,
            ILogger<CustomerController> logger)
        {
            _userService = userService;
            _orderService = orderService;
            _menuItemService = menuItemService;
            _logger = logger;
        }

        private IActionResult ValidateCustomerRole()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value; // ✅ FIXED
            if (role != "customer")
                return Unauthorized();
            return null;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            if (ValidateCustomerRole() is IActionResult unauthorized)
                return unauthorized;

            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var customer = await _userService.GetUserByIdAsync(userId);
            if (customer == null || customer.Role.ToLower() != "customer")
                return NotFound();

            return Ok(new
            {
                customer.Id,
                customer.Name,
                customer.Email
            });
        }

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] int limit = 3, [FromQuery] string status = null)
        {
            if (ValidateCustomerRole() is IActionResult unauthorized)
                return unauthorized;

            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            try
            {
                var orders = await _orderService.GetOrdersByCustomerIdAsync(userId, limit, status);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching orders for customer ID {CustomerId}. Query Params - Status: {Status}, Limit: {Limit}", userId, status, limit);
                return StatusCode(500, new { message = "An error occurred while retrieving your orders." });
            }
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            if (ValidateCustomerRole() is IActionResult unauthorized)
                return unauthorized;

            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var favorites = await _menuItemService.GetTopSellingMenuItemsAsync(limit: 5);
            return Ok(favorites);
        }

        [HttpGet("notifications")]
        public IActionResult GetNotifications([FromQuery] int limit = 5)
        {
            if (ValidateCustomerRole() is IActionResult unauthorized)
                return unauthorized;

            return Ok(new[]
            {
                new { message = "Your order #125 is ready for pickup!", createdAt = DateTime.UtcNow.AddMinutes(-5) },
                new { message = "Weekend promo: 10% off all drinks!", createdAt = DateTime.UtcNow.AddHours(-2) }
            }.Take(limit));
        }

        [HttpGet("menu")]
        public async Task<IActionResult> GetAvailableMenu()
        {
            if (ValidateCustomerRole() is IActionResult unauthorized)
                return unauthorized;

            var menuItems = await _menuItemService.GetAvailableMenuItemsAsync();
            return Ok(menuItems);
        }
    }
}