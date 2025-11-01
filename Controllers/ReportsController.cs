using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Services;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetReportHistory()
        {
            var history = await _reportService.GetReportHistoryAsync();
            return Ok(history);
        }

        [HttpPost("sales-report")]
        public async Task<IActionResult> GenerateSalesReport([FromBody] SalesReportRequest request)
        {
            if (string.IsNullOrEmpty(request.StartDate) || string.IsNullOrEmpty(request.EndDate))
                return BadRequest("StartDate and EndDate are required.");

            var report = await _reportService.GenerateSalesReportAsync(request);
            return Ok(report);
        }

        [HttpGet("inventory-report")]
        public async Task<IActionResult> GenerateInventoryReport()
        {
            var report = await _reportService.GenerateInventoryReportAsync();
            return Ok(report);
        }
    }
}