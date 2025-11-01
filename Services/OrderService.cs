using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class OrderService
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;

        public OrderService(
            IMongoDatabase database,
            ProductService productService,
            InventoryService inventoryService)
        {
            _orders = database.GetCollection<Order>("orders");
            _productService = productService;
            _inventoryService = inventoryService;
        }

        public void Create(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            // ✅ CRITICAL: Always set CreatedAt to current UTC time
            order.CreatedAt = DateTime.UtcNow;

            DeductInventoryForOrder(order);
            _orders.InsertOne(order);
        }

        private void DeductInventoryForOrder(Order order)
        {
            foreach (var orderItem in order.Items)
            {
                var product = _productService.GetAll().FirstOrDefault(p => p.Id == orderItem.ProductId);
                if (product == null) continue;

                foreach (var ingredient in product.Ingredients)
                {
                    var inventoryItem = _inventoryService.GetAll()
                        .FirstOrDefault(i => i.Id == ingredient.InventoryItemId);
                    if (inventoryItem == null) continue;

                    var needed = ingredient.QuantityRequired * orderItem.Quantity;
                    if (inventoryItem.CurrentStock < needed)
                    {
                        throw new InvalidOperationException(
                            $"Not enough stock for '{inventoryItem.Name}'. Required: {needed}, Available: {inventoryItem.CurrentStock}");
                    }
                }
            }

            foreach (var orderItem in order.Items)
            {
                var product = _productService.GetAll().FirstOrDefault(p => p.Id == orderItem.ProductId);
                if (product == null) continue;

                foreach (var ingredient in product.Ingredients)
                {
                    var filter = Builders<InventoryItem>.Filter.Eq(i => i.Id, ingredient.InventoryItemId);
                    var update = Builders<InventoryItem>.Update.Inc(i => i.CurrentStock, -ingredient.QuantityRequired * orderItem.Quantity);
                    _inventoryService.GetCollection().UpdateOne(filter, update);
                }
            }
        }

        public async Task<List<Order>> GetAllOrdersAsync() =>
            await _orders.Find(_ => true).ToListAsync();

        public List<Order> GetAll() => _orders.Find(_ => true).ToList();

        public long GetTotalCount() =>
            _orders.CountDocuments(_ => true);

        public decimal GetTotalRevenue()
        {
            var orders = _orders.Find(_ => true).ToList();
            return orders.Sum(o => o.TotalAmount);
        }

        public long GetPendingCount() =>
            _orders.CountDocuments(o => !o.IsCompleted);

        public List<TopSellingItemDto> GetTopSellingItemsWithDetails()
        {
            var allOrders = _orders.Find(o => o.IsCompleted).ToList();
            var allProducts = _productService.GetAll();

            var itemStats = allOrders
                .SelectMany(o => o.Items)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    QuantitySold = g.Sum(oi => oi.Quantity),
                    TotalRevenue = g.Sum(oi => oi.PriceAtOrder * oi.Quantity)
                })
                .ToList();

            var result = new List<TopSellingItemDto>();
            foreach (var stat in itemStats)
            {
                var product = allProducts.FirstOrDefault(p => p.Id == stat.ProductId);
                if (product != null)
                {
                    result.Add(new TopSellingItemDto
                    {
                        Name = product.Name,
                        QuantitySold = stat.QuantitySold,
                        TotalRevenue = stat.TotalRevenue
                    });
                }
            }

            return result.OrderByDescending(x => x.QuantitySold).ToList();
        }

        public CustomerInsightsDto GetCustomerInsights()
        {
            var allOrders = _orders.Find(_ => true).ToList();
            var emails = allOrders
                .Where(o => !string.IsNullOrWhiteSpace(o.CustomerEmail))
                .Select(o => o.CustomerEmail)
                .ToList();

            if (!emails.Any())
                return new CustomerInsightsDto { NewCustomers = 0, RepeatCustomers = 0, RetentionRate = 0.0 };

            var groups = emails.GroupBy(e => e);
            var repeat = groups.Count(g => g.Count() > 1);
            var total = groups.Count();
            var retention = (double)repeat / total;

            return new CustomerInsightsDto
            {
                NewCustomers = total - repeat,
                RepeatCustomers = repeat,
                RetentionRate = retention
            };
        }

        public ProfitLossReportDto GetProfitLossReport()
        {
            var revenue = GetTotalRevenue();
            return new ProfitLossReportDto
            {
                TotalRevenue = revenue,
                TotalExpenses = revenue * 0.4m
            };
        }
    }
}