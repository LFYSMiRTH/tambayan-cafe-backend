using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace TambayanCafeAPI.Services
{
    public class OrderService : IOrderService
    {
        private readonly IMongoCollection<Order> _orders;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;
        private readonly NotificationService _notificationService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IMongoDatabase database,
            ProductService productService,
            InventoryService inventoryService,
            NotificationService notificationService,
            ILogger<OrderService> logger)
        {
            _orders = database.GetCollection<Order>("orders");
            _productService = productService;
            _inventoryService = inventoryService;
            _notificationService = notificationService;
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
                var menuItem = _productService.GetById(item.ProductId);
                if (menuItem == null)
                {
                    throw new ArgumentException($"Menu item with ID {item.ProductId} not found.", nameof(orderRequest));
                }
                // Optional: Check if price matches the item record
                if (Math.Abs(item.Price - menuItem.Price) > 0.01m)
                {
                    _logger.LogWarning("Price mismatch for item {ProductId}. Requested: {RequestedPrice}, Actual: {ActualPrice}", item.ProductId, item.Price, menuItem.Price);
                }
            }

            // Calculate total amount again (optional, for security) - frontend provides it, but server should verify
            var calculatedTotal = orderRequest.Items.Sum(item => item.Price * item.Quantity);
            if (Math.Abs(calculatedTotal - orderRequest.TotalAmount) > 0.01m)
            {
                throw new ArgumentException("Calculated total does not match provided total.", nameof(orderRequest));
            }

            // Create the Order object
            var order = new Order
            {
                OrderNumber = GenerateOrderNumber(),
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
                Status = "Pending",
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            // Deduct inventory and save the order
            try
            {
                DeductInventoryForOrder(order);
                await _orders.InsertOneAsync(order);

                // 🔔 NEW: Create notification for new order
                var notification = new Notification
                {
                    Message = $"🧾 New order #{order.OrderNumber} received.",
                    Type = "info",
                    Category = "order",
                    RelatedId = order.Id,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationService.CreateAsync(notification);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Inventory error while creating order for customer {CustomerId}", order.CustomerId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database error while creating order for customer {CustomerId}", order.CustomerId);
                throw;
            }

            return order;
        }

        private string GenerateOrderNumber()
        {
            return "ORD" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        }

        private void DeductInventoryForOrder(Order order)
        {
            // Phase 1: Validate stock availability for all items in the order
            foreach (var orderItem in order.Items)
            {
                var product = _productService.GetById(orderItem.ProductId);
                if (product == null)
                {
                    _logger.LogWarning("Product {ProductId} not found during inventory validation for Order {OrderId}. Skipping order.", orderItem.ProductId, order.Id);
                    throw new InvalidOperationException($"Product with ID {orderItem.ProductId} not found during inventory validation.");
                }

                // 🔥 NEW: Check product-level stockQuantity
                if (product.StockQuantity < orderItem.Quantity)
                {
                    throw new InvalidOperationException(
                        $"Not enough stock for product '{product.Name}'. Required: {orderItem.Quantity}, Available: {product.StockQuantity}");
                }

                // Validate ingredient-level stock (existing logic)
                foreach (var ingredient in product.Ingredients)
                {
                    var inventoryItem = _inventoryService.GetById(ingredient.InventoryItemId);
                    if (inventoryItem == null)
                    {
                        _logger.LogWarning("Inventory item {InventoryItemId} for product {ProductId} not found for Order {OrderId}.", ingredient.InventoryItemId, orderItem.ProductId, order.Id);
                        throw new InvalidOperationException($"Inventory item '{ingredient.InventoryItemId}' for product '{orderItem.ProductId}' not found.");
                    }

                    var needed = ingredient.QuantityRequired * orderItem.Quantity;
                    // 🔥 IMPROVED: Round needed quantity for "pcs" unit (since pcs must be whole)
                    int neededPcs = (int)Math.Ceiling(needed); // e.g., 2.1 → 3 pcs
                    if (inventoryItem.CurrentStock < neededPcs)
                    {
                        throw new InvalidOperationException(
                            $"Not enough stock for '{inventoryItem.Name}'. Required: {neededPcs} {ingredient.Unit}, Available: {inventoryItem.CurrentStock}");
                    }
                }
            }

            // Phase 2: If validation passes, perform the actual stock deduction

            // 🔥 NEW: Deduct product-level StockQuantity
            foreach (var orderItem in order.Items)
            {
                var productFilter = Builders<Product>.Filter.Eq("_id", ObjectId.Parse(orderItem.ProductId));
                var productUpdate = Builders<Product>.Update.Inc(p => p.StockQuantity, -orderItem.Quantity);
                var productResult = _productService.GetCollection().UpdateOne(productFilter, productUpdate);

                if (productResult.MatchedCount == 0)
                {
                    _logger.LogWarning("No product found to update stock for ID {ProductId} in Order {OrderId}. Product-level deduction failed.", orderItem.ProductId, order.Id);
                }
                else if (productResult.ModifiedCount == 0)
                {
                    _logger.LogWarning("Product stock update succeeded but no changes made for ID {ProductId} in Order {OrderId}. May have been race condition.", orderItem.ProductId, order.Id);
                }

                // Existing: Deduct ingredient-level inventory
                var product = _productService.GetById(orderItem.ProductId);
                if (product == null)
                {
                    _logger.LogError("Product {ProductId} not found during inventory deduction for Order {OrderId} (second loop). This should not occur if validation passed.", orderItem.ProductId, order.Id);
                    continue;
                }

                foreach (var ingredient in product.Ingredients)
                {
                    var inventoryItem = _inventoryService.GetById(ingredient.InventoryItemId);
                    if (inventoryItem == null)
                    {
                        _logger.LogWarning("Inventory item missing during deduction: {InventoryItemId}", ingredient.InventoryItemId);
                        continue;
                    }

                    // 🔥 IMPROVED: Use Math.Ceiling for "pcs" — ensures no fractional items
                    decimal needed = ingredient.QuantityRequired * orderItem.Quantity;
                    int deduction = (int)Math.Ceiling(needed);

                    // 🔥 NEW: Log the deduction for visibility
                    _logger.LogInformation(
                        "Deducting {Deduction} {Unit} of '{IngredientName}' (ID: {InventoryId}) for {OrderQuantity}x '{ProductName}' (Order: {OrderId})",
                        deduction,
                        ingredient.Unit,
                        inventoryItem.Name,
                        ingredient.InventoryItemId,
                        orderItem.Quantity,
                        product.Name,
                        order.Id);

                    var filter = Builders<InventoryItem>.Filter.Eq("_id", ObjectId.Parse(ingredient.InventoryItemId));
                    var update = Builders<InventoryItem>.Update.Inc(i => i.CurrentStock, -deduction);
                    var result = _inventoryService.GetCollection().UpdateOne(filter, update);

                    if (result.MatchedCount == 0)
                    {
                        _logger.LogWarning("No inventory item found to update for ID {InventoryItemId} in Order {OrderId}. Deduction failed.", ingredient.InventoryItemId, order.Id);
                    }
                    else if (result.ModifiedCount == 0)
                    {
                        _logger.LogWarning("Inventory update succeeded but no change for {InventoryId} — possible race condition.", ingredient.InventoryItemId);
                    }
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
            var effectiveLimit = limit <= 0 ? int.MaxValue : limit;
            return await _orders
                .Find(filter)
                .SortByDescending(o => o.CreatedAt)
                .Limit(effectiveLimit)
                .ToListAsync();
        }
    }
}