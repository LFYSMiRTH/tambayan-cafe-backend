using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using Microsoft.Extensions.Logging;

namespace TambayanCafeAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IMongoDatabase database,
            ProductService productService,
            InventoryService inventoryService,
            ILogger<OrderService> logger)
        {
            _orders = database.GetCollection<Order>("orders");
            _productService = productService;
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(OrderRequestDto orderRequest)
        {
            if (orderRequest == null)
                throw new ArgumentNullException(nameof(orderRequest));

            // Validate customer ID exists (optional, depending on your auth flow)
            if (string.IsNullOrEmpty(orderRequest.CustomerId))
                throw new ArgumentException("Customer ID is required.", nameof(orderRequest));

            // Validate menu items exist and prices are correct (optional, for security)
            foreach (var item in orderRequest.Items)
            {
                var menuItem = _productService.GetById(item.ProductId); // Assuming ProductService has GetById
                if (menuItem == null)
                {
                    throw new ArgumentException($"Menu item with ID {item.ProductId} not found.", nameof(orderRequest));
                }
                // Optional: Check if price matches the item record
                if (Math.Abs(item.Price - menuItem.Price) > 0.01m) // Tolerance for floating point
                {
                    _logger.LogWarning("Price mismatch for item {ProductId}. Requested: {RequestedPrice}, Actual: {ActualPrice}", item.ProductId, item.Price, menuItem.Price);
                    // Decide: Throw exception, use actual price, or log and continue
                    // For now, let's proceed with the requested price as sent by frontend
                    // item.Price = menuItem.Price; // Uncomment if you want to enforce actual price
                }
            }

            // Calculate total amount again (optional, for security) - frontend provides it, but server should verify
            var calculatedTotal = orderRequest.Items.Sum(item => item.Price * item.Quantity);
            if (Math.Abs(calculatedTotal - orderRequest.TotalAmount) > 0.01m) // Tolerance for floating point
            {
                throw new ArgumentException("Calculated total does not match provided total.", nameof(orderRequest));
            }

            // Create the Order object
            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(), // Implement this logic
                CustomerId = orderRequest.CustomerId,
                CustomerEmail = orderRequest.CustomerEmail,
                Items = orderRequest.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    Size = item.Size,
                    Mood = item.Mood,
                    Sugar = item.Sugar
                }).ToList(),
                TotalAmount = orderRequest.TotalAmount,
                Status = "Pending", // Initial status
                IsCompleted = false, // Default
                CreatedAt = DateTime.UtcNow
            };

            // Deduct inventory and save the order
            try
            {
                DeductInventoryForOrder(order);
                await _orders.InsertOneAsync(order);
            }
            catch (InvalidOperationException ex) // Handle inventory errors specifically
            {
                _logger.LogError(ex, "Inventory error while creating order for customer {CustomerId}", order.CustomerId);
                throw; // Re-throw to be caught by controller
            }
            catch (Exception ex) // Handle other database errors
            {
                _logger.LogError(ex, "Database error while creating order for customer {CustomerId}", order.CustomerId);
                throw; // Re-throw to be caught by controller
            }

            return order;
        }

        // Example method to generate a unique order number
        private string GenerateOrderNumber()
        {
            // Implement your logic for generating a unique order number
            // e.g., "ORD" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Random.Next(100, 999);
            return "ORD" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
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

        public async Task<List<Order>> GetOrdersByCustomerIdAsync(string customerId, int limit = 3, string status = null)
        {
            var filter = Builders<Order>.Filter.Eq(o => o.CustomerId, customerId);
            if (!string.IsNullOrEmpty(status))
            {
                var statuses = status.Split(',');
                var statusFilters = statuses.Select(s => Builders<Order>.Filter.Eq(o => o.Status, s.Trim())).ToList();
                var combinedStatusFilter = Builders<Order>.Filter.Or(statusFilters);
                filter = Builders<Order>.Filter.And(filter, combinedStatusFilter);
            }
            // Ensure limit is at least 1 to prevent potential issues with MongoDB driver
            var effectiveLimit = limit <= 0 ? int.MaxValue : limit;
            return await _orders
                .Find(filter)
                .SortByDescending(o => o.CreatedAt)
                .Limit(effectiveLimit)
                .ToListAsync();
        }
    }
}