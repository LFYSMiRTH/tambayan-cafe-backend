using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services; 

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AnalyticsController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        public AnalyticsController(OrderService orderService, InventoryService inventoryService)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
        }

        [HttpGet("top-selling-detailed")]
        public IActionResult GetTopSellingDetailed()
        {
            var items = _orderService.GetTopSellingItemsWithDetails();
            return Ok(items);
        }

        [HttpGet("customer-insights")]
        public IActionResult GetCustomerInsights()
        {
            var insights = _orderService.GetCustomerInsights();
            return Ok(insights);
        }

        [HttpGet("profit-loss")]
        public IActionResult GetProfitLossReport()
        {
            var report = _orderService.GetProfitLossReport();
            return Ok(report);
        }

        [HttpGet("expenses")]
        public IActionResult GetExpenses()
        {
            return Ok(new List<ExpenseDto>());
        }
    }
}