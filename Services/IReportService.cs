using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TambayanCafeAPI.Services
{
    public interface IReportService
    {
        Task<List<ReportHistoryItem>> GetReportHistoryAsync();
        Task<SalesReportResponse> GenerateSalesReportAsync(SalesReportRequest request);
        Task<InventoryReportResponse> GenerateInventoryReportAsync();
    }

    public class ReportHistoryItem
    {
        public string Title { get; set; }
        public string Type { get; set; }
        public string Format { get; set; }
        public string GeneratedAt { get; set; }
    }

    public class SalesReportRequest
    {
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }

    public class SalesReportResponse
    {
        public List<SalesOrderItem> Sales { get; set; } = new();
    }

    public class SalesOrderItem
    {
        public string Date { get; set; }
        public string OrderId { get; set; }
        public List<OrderedItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
    }

    public class OrderedItem
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class InventoryReportResponse
    {
        public List<InventoryReportItem> Inventory { get; set; } = new();
    }

    // ✅ Renamed to avoid conflict with Models.InventoryItem
    public class InventoryReportItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public int CurrentStock { get; set; }
        public string Unit { get; set; }
        public int ReorderLevel { get; set; }
    }
}