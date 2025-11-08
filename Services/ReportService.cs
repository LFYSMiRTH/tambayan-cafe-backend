using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Helpers; // 👈 ADDED

namespace TambayanCafeAPI.Services
{
    public class ReportService : IReportService
    {
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly ProductService _productService;
        private readonly IMongoCollection<ReportLog> _reportLogs;

        public ReportService(
            OrderService orderService,
            InventoryService inventoryService,
            ProductService productService,
            IMongoDatabase database)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _productService = productService;
            _reportLogs = database.GetCollection<ReportLog>("reportLogs");
        }

        public async Task<List<ReportHistoryItem>> GetReportHistoryAsync()
        {
            var logs = await _reportLogs
                .Find(_ => true)
                .SortByDescending(x => x.GeneratedAt)
                .Limit(50)
                .ToListAsync();

            return logs.Select(log => new ReportHistoryItem
            {
                Title = log.Title,
                Type = log.Type,
                Format = log.Format,
                GeneratedAt = log.GeneratedAt
            }).ToList();
        }

        public async Task<SalesReportResponse> GenerateSalesReportAsync(SalesReportRequest request)
        {
            if (!DateTime.TryParse(request.StartDate, out var start) ||
                !DateTime.TryParse(request.EndDate, out var end))
                throw new ArgumentException("Invalid date format.");

            end = end.AddDays(1);

            var orders = await _orderService.GetAllOrdersAsync();
            var products = _productService.GetAll().ToDictionary(p => p.Id, p => p.Name);

            var filtered = orders.Where(o => o.CreatedAt >= start && o.CreatedAt < end).ToList();

            var sales = filtered.Select(order => new SalesOrderItem
            {
                Date = order.CreatedAt.ToString("yyyy-MM-dd"),
                OrderId = order.Id ?? "N/A",
                Items = order.Items?.Select(item => new OrderedItem
                {
                    Name = products.TryGetValue(item.ProductId, out var name) ? name : "Unknown",
                    Quantity = item.Quantity
                }).ToList() ?? new List<OrderedItem>(),
                TotalAmount = order.TotalAmount,
                Status = order.IsCompleted ? "Completed" : "Pending"
            }).ToList();

            return new SalesReportResponse { Sales = sales };
        }

        public async Task<InventoryReportResponse> GenerateInventoryReportAsync()
        {
            var items = await _inventoryService.GetAllInventoryItemsAsync();

            var reportItems = items.Select(i => new InventoryReportItem
            {
                Name = i.Name,
                Category = i.Category ?? "Uncategorized",
                CurrentStock = i.CurrentStock,
                Unit = i.Unit ?? "unit",
                ReorderLevel = i.ReorderLevel
            }).ToList();

            return new InventoryReportResponse { Inventory = reportItems };
        }

        // 👇👇👇 NEW METHOD — ADDED BELOW (NO EXISTING CODE CHANGED) 👇👇👇
        public async Task<List<SalesTrendDto>> GetSalesTrendDataAsync(string period, bool isPrevious)
        {
            var now = DateTime.UtcNow;

            // Determine date range based on period and isPrevious
            (DateTime startDate, DateTime endDate) = period.ToLower() switch
            {
                "weekly" => isPrevious
                    ? (now.AddDays(-14).StartOfWeek(), now.AddDays(-7).EndOfWeek())
                    : (now.AddDays(-7).StartOfWeek(), now.EndOfWeek()),

                "monthly" => isPrevious
                    ? (now.AddMonths(-2).StartOfMonth(), now.AddMonths(-1).EndOfMonth())
                    : (now.AddMonths(-1).StartOfMonth(), now.EndOfMonth()),

                _ => isPrevious // yearly
                    ? (new DateTime(now.Year - 2, 1, 1), new DateTime(now.Year - 1, 12, 31, 23, 59, 59, 999))
                    : (new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year, 12, 31, 23, 59, 59, 999))
            };

            // Fetch all orders (you already have this service method)
            var allOrders = await _orderService.GetAllOrdersAsync();

            // Filter by date range — using CreatedAt (match your Order model)
            var filteredOrders = allOrders
                .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
                .ToList();

            // Group by time unit
            var grouped = filteredOrders
                .GroupBy(o => period.ToLower() switch
                {
                    "weekly" => o.CreatedAt.StartOfWeek().ToString("MM/dd"),
                    "monthly" => o.CreatedAt.ToString("MMM yyyy"),
                    "yearly" => o.CreatedAt.Year.ToString(),
                    _ => o.CreatedAt.ToString("yyyy-MM-dd")
                })
                .Select(g => new SalesTrendDto
                {
                    Label = g.Key,
                    Value = g.Sum(o => o.TotalAmount)
                })
                .OrderBy(x => x.Label)
                .ToList();

            return grouped;
        }
    }
}