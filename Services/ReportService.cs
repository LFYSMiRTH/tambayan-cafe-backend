using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class ReportService : IReportService
    {
        private readonly OrderService _orderService;
        private readonly InventoryService _inventoryService;
        private readonly ProductService _productService;

        public ReportService(
            OrderService orderService,
            InventoryService inventoryService,
            ProductService productService)
        {
            _orderService = orderService;
            _inventoryService = inventoryService;
            _productService = productService;
        }

        public async Task<List<ReportHistoryItem>> GetReportHistoryAsync()
        {
            return new List<ReportHistoryItem>
            {
                new() { Title = "Sales Report (Jan 2025)", Type = "sales", Format = "PDF", GeneratedAt = "2025-01-15T10:00:00" },
                new() { Title = "Inventory Report", Type = "inventory", Format = "CSV", GeneratedAt = "2025-01-10T14:30:00" }
            };
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