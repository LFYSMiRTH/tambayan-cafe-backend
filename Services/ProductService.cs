using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace TambayanCafeAPI.Services
{
    public class ProductService : IMenuItemService, IProductService
    {
        private readonly IMongoCollection<Product> _products;
        private readonly InventoryService _inventoryService;
        private readonly ILogger<ProductService> _logger;

        public ProductService(IMongoDatabase database, InventoryService inventoryService, ILogger<ProductService> logger = null)
        {
            _products = database.GetCollection<Product>("products");
            _inventoryService = inventoryService;
            _logger = logger;
        }

        public void Create(Product product) => _products.InsertOne(product);

        public List<Product> GetAll() => _products.Find(_ => true).ToList();

        public Product GetById(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null;

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            return _products.Find(filter).FirstOrDefault();
        }

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

        public async Task<List<Product>> GetAvailableMenuItemsAsync()
        {
            var candidateProducts = await _products
                .Find(p => p.IsAvailable == true)
                .ToListAsync();

            var allInventory = await _inventoryService.GetAllInventoryItemsAsync();
            var inventoryDict = allInventory.ToDictionary(i => i.Id, i => i);

            var trulyAvailable = new List<Product>();

            foreach (var product in candidateProducts)
            {
                bool canFulfill = true;

                foreach (var ingredient in product.Ingredients)
                {
                    if (!inventoryDict.TryGetValue(ingredient.InventoryItemId, out var invItem))
                    {
                        canFulfill = false;
                        _logger?.LogWarning("Ingredient '{InventoryId}' not found for product '{ProductId}'", ingredient.InventoryItemId, product.Id);
                        break;
                    }

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

                if (!product.IsAvailable)
                {
                    dto.UnavailableReason = "Manually disabled";
                    result.Add(dto);
                    continue;
                }

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
            var available = await GetAvailableMenuItemsAsync();
            return available.Take(limit).ToList();
        }

        public async Task<Product> GetByIdAsync(string id)
        {
            return GetById(id);
        }

        public async Task UpdateAsync(Product product)
        {
            if (string.IsNullOrEmpty(product?.Id))
                throw new ArgumentException("Product ID is required.", nameof(product));

            Update(product.Id, product);
        }

        public async Task<List<Product>> GetAllAsync()
        {
            return GetAll();
        }

        public async Task<bool> TryDeductStockAsync(string productId, int quantity)
        {
            if (quantity <= 0) return false;

            var product = await GetByIdAsync(productId);
            if (product == null) return false;

            if (product.StockQuantity < quantity)
                return false;

            product.StockQuantity -= quantity;
            await UpdateAsync(product);
            return true;
        }
    }
}