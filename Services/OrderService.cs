using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;

namespace TambayanCafeSystem.Services
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

                    var totalNeeded = ingredient.QuantityRequired * orderItem.Quantity;
                    if (inventoryItem.CurrentStock < totalNeeded)
                    {
                        throw new InvalidOperationException(
                            $"Not enough stock for '{inventoryItem.Name}'. Required: {totalNeeded}, Available: {inventoryItem.CurrentStock}");
                    }
                }
            }

            foreach (var orderItem in order.Items)
            {
                var product = _productService.GetAll().FirstOrDefault(p => p.Id == orderItem.ProductId);
                if (product == null) continue;

                foreach (var ingredient in product.Ingredients)
                {
                    var filter = Builders<InventoryItem>.Filter.Eq("_id", ObjectId.Parse(ingredient.InventoryItemId));
                    var update = Builders<InventoryItem>.Update.Inc("currentStock", -ingredient.QuantityRequired * orderItem.Quantity);
                    _inventoryService.GetCollection().UpdateOne(filter, update);
                }
            }
        }

        public List<Order> GetAll()
        {
            return _orders.Find(_ => true).ToList();
        }

        public long GetTotalCount()
        {
            return _orders.CountDocuments(_ => true);
        }

        public decimal GetTotalRevenue()
        {
            var orders = _orders.Find(_ => true).ToList();
            return orders.Sum(order => order.TotalAmount);
        }

        public long GetPendingCount()
        {
            return _orders.CountDocuments(order => !order.IsCompleted);
        }

        public List<TopSellingItemDto> GetTopSellingItemsWithDetails()
        {
            var allOrders = _orders.Find(order => order.IsCompleted).ToList();
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
                        TotalRevenue = stat.TotalRevenue,
                        AvgRating = null
                    });
                }
            }

            return result.OrderByDescending(x => x.QuantitySold).ToList();
        }

        public CustomerInsightsDto GetCustomerInsights()
        {
            var allOrders = _orders.Find(_ => true).ToList();
            var customerEmails = allOrders
                .Where(o => !string.IsNullOrWhiteSpace(o.CustomerEmail)) 
                .Select(o => o.CustomerEmail)
                .ToList();

            if (!customerEmails.Any())
            {
                return new CustomerInsightsDto
                {
                    NewCustomers = 0,
                    RepeatCustomers = 0,
                    RetentionRate = 0.0
                };
            }

            var emailGroups = customerEmails.GroupBy(email => email);
            var repeatCustomers = emailGroups.Count(g => g.Count() > 1);
            var totalUnique = emailGroups.Count();
            var newCustomers = totalUnique - repeatCustomers;
            var retentionRate = totalUnique > 0 ? (double)repeatCustomers / totalUnique : 0.0;

            return new CustomerInsightsDto
            {
                NewCustomers = newCustomers,
                RepeatCustomers = repeatCustomers,
                RetentionRate = retentionRate
            };
        }

        public ProfitLossReportDto GetProfitLossReport()
        {
            var totalRevenue = GetTotalRevenue();
            var estimatedExpenses = totalRevenue * 0.4m; 

            return new ProfitLossReportDto
            {
                TotalRevenue = totalRevenue,
                TotalExpenses = estimatedExpenses
            };
        }
    }
}