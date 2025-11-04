using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/customer")]
    [Authorize(Roles = "customer")]
    public class CustomerController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IOrderService _orderService;
        private readonly IMenuItemService _menuItemService;

        public CustomerController(
            IUserService userService,
            IOrderService orderService,
            IMenuItemService menuItemService)
        {
            _userService = userService;
            _orderService = orderService;
            _menuItemService = menuItemService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var customer = await _userService.GetUserByIdAsync(userId);
            if (customer == null || customer.Role != "customer")
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
            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var orders = await _orderService.GetOrdersByCustomerIdAsync(userId, limit, status);
            return Ok(orders);
        }

        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Recommend top 5 best-selling items (you can later refine with personalization)
            var favorites = await _menuItemService.GetTopSellingMenuItemsAsync(limit: 5);
            return Ok(favorites);
        }

        [HttpGet("notifications")]
        public IActionResult GetNotifications([FromQuery] int limit = 5)
        {
            // Placeholder: In real app, fetch from NotificationService
            return Ok(new[]
            {
                new { message = "Your order #125 is ready for pickup!", createdAt = DateTime.UtcNow.AddMinutes(-5) },
                new { message = "Weekend promo: 10% off all drinks!", createdAt = DateTime.UtcNow.AddHours(-2) }
            }.Take(limit));
        }

        // ✅ NEW ENDPOINT: Returns ONLY available menu items for customers
        [HttpGet("menu")]
        public async Task<IActionResult> GetAvailableMenu()
        {
            var menuItems = await _menuItemService.GetAvailableMenuItemsAsync();
            return Ok(menuItems);
        }
    }
}