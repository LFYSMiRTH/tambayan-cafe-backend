using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TambayanCafeAPI.Services;
using TambayanCafeAPI.Models; 
using MongoDB.Driver;         

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly IMongoCollection<ReportLog> _reportLogs;

        public ReportsController(
            IReportService reportService,
            IMongoDatabase database)
        {
            _reportService = reportService;
            _reportLogs = database.GetCollection<ReportLog>("reportLogs");
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetReportHistory()
        {
            var history = await _reportService.GetReportHistoryAsync();
            return Ok(history);
        }

        [HttpPost("history")]
        public async Task<IActionResult> SaveReportHistory([FromBody] ReportHistoryItem item)
        {
            if (string.IsNullOrEmpty(item.Title) || string.IsNullOrEmpty(item.Type))
                return BadRequest("Title and Type are required.");

            var log = new ReportLog
            {
                Title = item.Title,
                Type = item.Type,
                Format = item.Format ?? "generated",
                GeneratedAt = DateTime.UtcNow.ToString("o") 
            };

            await _reportLogs.InsertOneAsync(log);
            return Ok(new { success = true, id = log.Id });
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