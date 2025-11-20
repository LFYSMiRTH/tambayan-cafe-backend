using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class NotificationController : ControllerBase
    {
        private readonly NotificationService _notificationService;
        private readonly ILogger<NotificationController> _logger;

        public NotificationController(NotificationService notificationService, ILogger<NotificationController> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpGet("customer/notifications")]
        [Authorize(Roles = "customer")]
        public async Task<IActionResult> GetCustomerNotifications([FromQuery] int limit = 5)
        {
            try
            {
                var customerId = User.FindFirst("id")?.Value; // Get customer ID from JWT token
                if (string.IsNullOrEmpty(customerId))
                {
                    return Unauthorized("Customer ID not found in token.");
                }

                var notifications = await _notificationService.GetNotificationsForCustomerAsync(customerId, limit);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for customer");
                return StatusCode(500, new { message = "An error occurred while retrieving notifications." });
            }
        }

        [HttpGet("staff/notifications")]
        [Authorize(Roles = "staff,admin")]
        public async Task<IActionResult> GetStaffNotifications([FromQuery] int limit = 5)
        {
            try
            {
                var notifications = await _notificationService.GetNotificationsForRoleAsync("staff", limit);
                return Ok(notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving notifications for staff");
                return StatusCode(500, new { message = "An error occurred while retrieving notifications." });
            }
        }
    }
}