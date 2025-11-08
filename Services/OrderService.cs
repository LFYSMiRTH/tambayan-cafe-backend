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

            if (string.IsNullOrEmpty(orderRequest.CustomerId))
                throw new ArgumentException("Customer ID is required.", nameof(orderRequest));

            foreach (var item in orderRequest.Items)
            {
                var product = _productService.GetById(item.ProductId);
                if (product == null)
                {
                    throw new ArgumentException($"Product with ID {item.ProductId} not found.", nameof(orderRequest));
                }
                if (Math.Abs(item.Price - product.Price) > 0.01m)
                {
                    _logger.LogWarning("Price mismatch for item {ProductId}. Requested: {RequestedPrice}, Actual: {ActualPrice}", item.ProductId, item.Price, product.Price);
                }
            }

            var calculatedTotal = orderRequest.Items.Sum(item => item.Price * item.Quantity);
            if (Math.Abs(calculatedTotal - orderRequest.TotalAmount) > 0.01m)
            {
                throw new ArgumentException("Calculated total does not match provided total.", nameof(orderRequest));
            }

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

            try
            {
                await DeductInventoryForOrderAsync(order);
                await _orders.InsertOneAsync(order);

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

        // 🔥 FULLY REWRITTEN: Hybrid stock deduction (product + ingredients)
        private async Task DeductInventoryForOrderAsync(Order order)
        {
            foreach (var orderItem in order.Items)
            {
                var product = _productService.GetById(orderItem.ProductId);
                if (product == null)
                {
                    throw new InvalidOperationException($"Product with ID {orderItem.ProductId} not found.");
                }

                var requestedQty = orderItem.Quantity;
                var remainingQty = requestedQty;
                var hasIngredients = product.Ingredients != null && product.Ingredients.Any();
                var hasProductStock = product.StockQuantity > 0;

                // 🔹 STEP 1: Use pre-made stock FIRST (if available)
                if (hasProductStock)
                {
                    var useFromStock = Math.Min(product.StockQuantity, remainingQty);

                    if (useFromStock > 0)
                    {
                        // Atomic: deduct from product stock
                        var filter = Builders<Product>.Filter.And(
                            Builders<Product>.Filter.Eq("_id", ObjectId.Parse(orderItem.ProductId)),
                            Builders<Product>.Filter.Gte(p => p.StockQuantity, useFromStock)
                        );
                        var update = Builders<Product>.Update.Inc(p => p.StockQuantity, -useFromStock);
                        var result = await _productService.GetCollection().UpdateOneAsync(filter, update);

                        if (result.MatchedCount == 0)
                        {
                            throw new InvalidOperationException(
                                $"❌ Not enough pre-made '{product.Name}': need {useFromStock}, have {product.StockQuantity}");
                        }

                        remainingQty -= useFromStock;
                        _logger.LogInformation(
                            "✅ Used {Qty} pre-made '{Product}' (stock: {Old} → {New})",
                            useFromStock, product.Name, product.StockQuantity, product.StockQuantity - useFromStock);
                    }
                }

                // 🔹 STEP 2: If still need more, make from ingredients
                if (remainingQty > 0)
                {
                    if (!hasIngredients)
                    {
                        throw new InvalidOperationException(
                            $"❌ Cannot fulfill remaining {remainingQty}x '{product.Name}': no ingredients defined.");
                    }

                    // Validate & deduct ingredients for remainingQty
                    foreach (var ingredient in product.Ingredients)
                    {
                        var inventoryItem = _inventoryService.GetById(ingredient.InventoryItemId);
                        if (inventoryItem == null)
                        {
                            throw new InvalidOperationException($"Inventory item '{ingredient.InventoryItemId}' not found for '{product.Name}'.");
                        }

                        decimal totalNeeded = ingredient.QuantityRequired * remainingQty;
                        if (string.Equals(ingredient.Unit, "pcs", StringComparison.OrdinalIgnoreCase))
                        {
                            totalNeeded = Math.Ceiling(totalNeeded);
                        }

                        // 🔥 Atomic ingredient deduction
                        var filter = Builders<InventoryItem>.Filter.And(
                            Builders<InventoryItem>.Filter.Eq("_id", ObjectId.Parse(ingredient.InventoryItemId)),
                            Builders<InventoryItem>.Filter.Gte(i => i.CurrentStock, totalNeeded)
                        );
                        var update = Builders<InventoryItem>.Update.Inc(i => i.CurrentStock, -totalNeeded);
                        var result = await _inventoryService.GetCollection().UpdateOneAsync(filter, update);

                        if (result.MatchedCount == 0)
                        {
                            var fresh = _inventoryService.GetById(ingredient.InventoryItemId);
                            throw new InvalidOperationException(
                                $"❌ Insufficient '{inventoryItem.Name}' to make {remainingQty}x '{product.Name}': need {totalNeeded} {ingredient.Unit}, have {fresh?.CurrentStock ?? 0m}");
                        }

                        _logger.LogInformation(
                            "✅ Used {TotalNeeded} {Unit} '{Ingredient}' to make {Qty}x '{Product}'",
                            totalNeeded, ingredient.Unit, inventoryItem.Name, remainingQty, product.Name);
                    }
                }

                // ✅ Success: full order fulfilled
                _logger.LogInformation("✅ Fulfilled {Requested}x '{Product}' ({FromStock} from stock, {FromIngredients} made fresh)",
                    requestedQty, product.Name,
                    requestedQty - remainingQty,
                    remainingQty);
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
                    TotalRevenue = g.Sum(oi => oi.Price * oi.Quantity)
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