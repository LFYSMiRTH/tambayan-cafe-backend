using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

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
    }
}