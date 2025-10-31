using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;

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
    }
}