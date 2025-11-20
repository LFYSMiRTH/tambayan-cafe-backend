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
                Status = "New",
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
                var hasIngredients = product.Ingredients != null && product.Ingredients.Any();
                var hasProductStock = product.StockQuantity > 0;

                if (hasProductStock)
                {
                    if (product.StockQuantity < requestedQty)
                    {
                        throw new InvalidOperationException(
                            $"❌ Only {product.StockQuantity} pre-made '{product.Name}' available, but {requestedQty} ordered.");
                    }

                    var filter = Builders<Product>.Filter.And(
                        Builders<Product>.Filter.Eq("_id", ObjectId.Parse(orderItem.ProductId)),
                        Builders<Product>.Filter.Gte(p => p.StockQuantity, requestedQty)
                    );
                    var update = Builders<Product>.Update.Inc(p => p.StockQuantity, -requestedQty);
                    var result = await _productService.GetCollection().UpdateOneAsync(filter, update);

                    if (result.MatchedCount == 0)
                    {
                        throw new InvalidOperationException(
                            $"❌ Failed to deduct {requestedQty} from '{product.Name}' stock (concurrent update?).");
                    }

                    _logger.LogInformation("✅ Deducted {Qty} from '{Product}' stock (now: {NewStock})",
                        requestedQty, product.Name, product.StockQuantity - requestedQty);
                }

                if (hasIngredients)
                {
                    foreach (var ingredient in product.Ingredients)
                    {
                        var inventoryItem = _inventoryService.GetById(ingredient.InventoryItemId);
                        if (inventoryItem == null)
                        {
                            throw new InvalidOperationException($"Inventory item '{ingredient.InventoryItemId}' not found for '{product.Name}'.");
                        }

                        decimal totalNeeded = ingredient.QuantityRequired * requestedQty;

                        if (string.Equals(ingredient.Unit, "pcs", StringComparison.OrdinalIgnoreCase))
                        {
                            totalNeeded = Math.Ceiling(totalNeeded);
                        }

                        var filter = Builders<InventoryItem>.Filter.And(
                            Builders<InventoryItem>.Filter.Eq("_id", ObjectId.Parse(ingredient.InventoryItemId)),
                            Builders<InventoryItem>.Filter.Gte(i => i.CurrentStock, totalNeeded)
                        );
                        var update = Builders<InventoryItem>.Update.Inc(i => i.CurrentStock, -totalNeeded);
                        var result = await _inventoryService.GetCollection().UpdateOneAsync(filter, update);

                        if (result.MatchedCount == 0)
                        {
                            var fresh = _inventoryService.GetById(ingredient.InventoryItemId);
                            var current = fresh?.CurrentStock ?? 0m;
                            throw new InvalidOperationException(
                                $"❌ Insufficient '{inventoryItem.Name}': need {totalNeeded} {ingredient.Unit} for {requestedQty}x '{product.Name}', have {current}");
                        }

                        _logger.LogInformation("✅ Deducted {TotalNeeded} {Unit} '{Ingredient}' for {Qty}x '{Product}'",
                            totalNeeded, ingredient.Unit, inventoryItem.Name, requestedQty, product.Name);
                    }
                }

                if (!hasProductStock && !hasIngredients)
                {
                    _logger.LogWarning("Product '{Product}' has no stock tracking — assuming unlimited.", product.Name);
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

        public async Task<object> GetStaffDashboardStatsAsync()
        {
            var startOfDay = DateTime.UtcNow.Date;
            var endOfDay = startOfDay.AddDays(1);

            var filterToday = Builders<Order>.Filter.And(
                Builders<Order>.Filter.Gte(o => o.CreatedAt, startOfDay),
                Builders<Order>.Filter.Lt(o => o.CreatedAt, endOfDay),
                Builders<Order>.Filter.Ne(o => o.Status, "Cancelled")
            );

            var filterTodayCompleted = Builders<Order>.Filter.And(
                filterToday,
                Builders<Order>.Filter.In(o => o.Status, new[] { "Completed", "Served" })
            );

            // Fixed: Use correct statuses for pending orders
            var filterPending = Builders<Order>.Filter.In(o => o.Status, new[] { "New", "Preparing", "Pending" });

            var totalOrdersToday = await _orders.CountDocumentsAsync(filterToday);

            var totalSalesToday = await _orders
                .Aggregate()
                .Match(filterTodayCompleted)
                .Group(o => 1, g => g.Sum(o => o.TotalAmount))
                .FirstOrDefaultAsync();

            var pendingOrders = await _orders.CountDocumentsAsync(filterPending);

            var lowStockThreshold = 5;
            var lowStockFilter = Builders<InventoryItem>.Filter.Lt(ii => ii.CurrentStock, lowStockThreshold);

            var lowStockAlerts = 0;

            return new
            {
                totalOrdersToday = totalOrdersToday,
                totalSalesToday = totalSalesToday,
                pendingOrders = pendingOrders,
                lowStockAlerts = lowStockAlerts
            };
        }

        public async Task<IEnumerable<Order>> GetOrdersForStaffAsync(int limit, string statusFilter)
        {
            _logger.LogInformation($"GetOrdersForStaffAsync called - limit: {limit}, statusFilter: '{statusFilter}'");

            var filter = Builders<Order>.Filter.Empty;

            if (!string.IsNullOrEmpty(statusFilter))
            {
                _logger.LogInformation($"Applying filter for status: '{statusFilter}'");

                // Handle "Complete" which should include both "Completed" and "Served"
                // Check for both capitalized and lowercase versions to match frontend input
                if (statusFilter.Equals("Complete", StringComparison.OrdinalIgnoreCase) ||
                    statusFilter.Equals("complete", StringComparison.OrdinalIgnoreCase)) // Added this check
                {
                    _logger.LogInformation("Filtering for Completed or Served orders");

                    // Use the property name which maps to the BSON element "status"
                    filter = Builders<Order>.Filter.In(o => o.Status, new[] { "Completed", "Served" });
                }
                else
                {
                    // For other statuses like "New", "Preparing", "Ready", split by comma if needed
                    var statuses = statusFilter.Split(',').Select(s => s.Trim()).ToArray();
                    _logger.LogInformation($"Filtering for statuses: [{string.Join(", ", statuses)}]");

                    // Use the property name which maps to the BSON element "status"
                    filter = Builders<Order>.Filter.In(o => o.Status, statuses);
                }
            }
            else
            {
                _logger.LogInformation("No status filter applied - returning all orders");
            }

            // Count total orders vs filtered orders to debug
            var totalOrdersCount = await _orders.CountDocumentsAsync(Builders<Order>.Filter.Empty);
            var filteredOrdersCount = await _orders.CountDocumentsAsync(filter);

            _logger.LogInformation($"Total orders in database: {totalOrdersCount}");
            _logger.LogInformation($"Orders matching filter: {filteredOrdersCount}");

            var orders = await _orders.Find(filter).SortByDescending(o => o.CreatedAt).Limit(limit).ToListAsync();

            _logger.LogInformation($"Returning {orders.Count} orders");

            foreach (var order in orders)
            {
                _logger.LogInformation($"Order {order.OrderNumber} has status: '{order.Status}'");
            }

            return orders;
        }

        public async Task<Order> UpdateOrderStatusAsync(string orderId, string newStatus)
        {
            var filter = Builders<Order>.Filter.Eq(o => o.Id, orderId);

            var isCompleted = (newStatus == "Completed" || newStatus == "Served");

            var update = Builders<Order>.Update
                .Set(o => o.Status, newStatus)
                .Set(o => o.IsCompleted, isCompleted);

            var result = await _orders.UpdateOneAsync(filter, update);

            if (result.MatchedCount == 0)
            {
                _logger?.LogWarning("Order with ID {OrderId} not found for status update.", orderId);
                return null;
            }

            var updatedOrder = await _orders.Find(filter).FirstOrDefaultAsync();
            return updatedOrder;
        }

        public async Task<Order> GetOrderByIdAsync(string orderId)
        {
            var filter = Builders<Order>.Filter.Eq(o => o.Id, orderId);
            var order = await _orders.Find(filter).FirstOrDefaultAsync();
            return order;
        }
    }
}