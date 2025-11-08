using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace TambayanCafeAPI.Services
{
    public class ProductService : IMenuItemService
    {
        private readonly IMongoCollection<Product> _products;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<ProductService> _logger;

        // 🔥 NEW: Inject InventoryService & ILogger
        public ProductService(IMongoDatabase database, InventoryService inventoryService, ILogger<ProductService> logger = null)
        {
            _products = database.GetCollection<Product>("products");
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public void Create(Product product) => _products.InsertOne(product);

        public List<Product> GetAll() => _products.Find(_ => true).ToList();

        // Add the GetById method
        public Product GetById(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null;

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            return _products.Find(filter).FirstOrDefault();
        }

        // 🔥 NEW: Expose collection for atomic updates (e.g., stock deduction in OrderService)
        public IMongoCollection<Product> GetCollection() => _products;

        public void Update(string id, Product product)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid product ID format.", nameof(id));

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            var update = Builders<Product>.Update
                .Set("name", product.Name)
                .Set("price", product.Price)
                .Set("stockQuantity", product.StockQuantity)
                .Set("lowStockThreshold", product.LowStockThreshold)
                .Set("category", product.Category ?? "")
                .Set("isAvailable", product.IsAvailable)
                .Set("imageUrl", product.ImageUrl ?? "")
                .Set("ingredients", product.Ingredients);

            _products.UpdateOne(filter, update);
        }

        public void Delete(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid product ID format.", nameof(id));

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            _products.DeleteOne(filter);
        }

        public long GetLowStockCount() =>
            _products.CountDocuments(p => p.StockQuantity <= (p.LowStockThreshold > 0 ? p.LowStockThreshold : 5));

        // 🔥 REPLACED: Smart availability — checks BOTH IsAvailable AND ingredient stock
        public async Task<List<Product>> GetAvailableMenuItemsAsync()
        {
            // 1. Get all products marked as manually available
            var candidateProducts = await _products
                .Find(p => p.IsAvailable == true)
                .ToListAsync();

            // 2. Get all inventory items in one query
            var allInventory = await _inventoryService.GetAllInventoryItemsAsync();
            var inventoryDict = allInventory.ToDictionary(i => i.Id, i => i);

            var trulyAvailable = new List<Product>();

            foreach (var product in candidateProducts)
            {
                bool canFulfill = true;

                // Check each ingredient
                foreach (var ingredient in product.Ingredients)
                {
                    if (!inventoryDict.TryGetValue(ingredient.InventoryItemId, out var invItem))
                    {
                        canFulfill = false;
                        _logger?.LogWarning("Ingredient '{InventoryId}' not found for product '{ProductId}'", ingredient.InventoryItemId, product.Id);
                        break;
                    }

                    // Can we make at least 1 unit?
                    if (invItem.CurrentStock < ingredient.QuantityRequired)
                    {
                        canFulfill = false;
                        break;
                    }
                }

                if (canFulfill)
                {
                    trulyAvailable.Add(product);
                }
            }

            return trulyAvailable;
        }

        // 🔥 NEW: For Admin UI — all products with computed availability & reason
        public async Task<List<ProductWithAvailabilityDto>> GetProductsWithAvailabilityAsync()
        {
            var allProducts = await _products.Find(_ => true).ToListAsync();
            var allInventory = await _inventoryService.GetAllInventoryItemsAsync();
            var inventoryDict = allInventory.ToDictionary(i => i.Id, i => i);

            var result = new List<ProductWithAvailabilityDto>();

            foreach (var product in allProducts)
            {
                var dto = new ProductWithAvailabilityDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    StockQuantity = product.StockQuantity,
                    IsManuallyAvailable = product.IsAvailable,
                    Ingredients = product.Ingredients,
                    IsAvailable = product.IsAvailable,
                    UnavailableReason = ""
                };

                // If manually disabled, skip auto-check
                if (!product.IsAvailable)
                {
                    dto.UnavailableReason = "Manually disabled";
                    result.Add(dto);
                    continue;
                }

                // Check ingredient sufficiency
                bool canFulfill = true;
                foreach (var ingredient in product.Ingredients)
                {
                    if (!inventoryDict.TryGetValue(ingredient.InventoryItemId, out var invItem))
                    {
                        canFulfill = false;
                        dto.UnavailableReason = $"Ingredient missing: {ingredient.InventoryItemId}";
                        break;
                    }

                    if (invItem.CurrentStock < ingredient.QuantityRequired)
                    {
                        canFulfill = false;
                        dto.UnavailableReason = $"Low stock: {invItem.Name} ({invItem.CurrentStock} < {ingredient.QuantityRequired}{ingredient.Unit})";
                        break;
                    }
                }

                dto.IsAvailable = canFulfill;
                if (!canFulfill && string.IsNullOrEmpty(dto.UnavailableReason))
                {
                    dto.UnavailableReason = "Insufficient ingredients";
                }

                result.Add(dto);
            }

            return result;
        }

        public async Task<List<Product>> GetTopSellingMenuItemsAsync(int limit = 5)
        {
            // Only return available items (smart-available)
            var available = await GetAvailableMenuItemsAsync();
            return available.Take(limit).ToList();
        }
    }
}