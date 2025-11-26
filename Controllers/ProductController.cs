using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using System.Linq;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IMongoCollection<Product> _products;
        private readonly IMongoCollection<InventoryItem> _inventoryItems;

        public ProductController(IMongoClient client)
        {
            var databaseName = "TambayanCafeDB";
            var db = client.GetDatabase(databaseName);
            _products = db.GetCollection<Product>("products");
            _inventoryItems = db.GetCollection<InventoryItem>("Inventory");
        }

        // ✅ PUBLIC: Get all menu items for CUSTOMER (enriched with ingredient names)
        [HttpGet("customer/menu")]
        public async Task<ActionResult<List<object>>> GetCustomerMenu()
        {
            var products = await _products.Find(p => p.IsAvailable).ToListAsync();
            return Ok(await EnrichProducts(products));
        }

        // ✅ STAFF: Get all menu items for STAFF (enriched, shows all products)
        [HttpGet("staff/menu")]
        public async Task<ActionResult<List<object>>> GetStaffMenu()
        {
            var products = await _products.Find(_ => true).ToListAsync();
            return Ok(await EnrichProducts(products));
        }

        // ✅ ADMIN: Get all menu items for ADMIN (enriched)
        [HttpGet("admin/menu")]
        public async Task<ActionResult<List<object>>> GetAdminMenu()
        {
            var products = await _products.Find(_ => true).ToListAsync();
            return Ok(await EnrichProducts(products));
        }

        // ✅ ADMIN: Get single product (enriched)
        [HttpGet("admin/menu/{id}")]
        public async Task<ActionResult<object>> GetAdminProduct(string id)
        {
            var product = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (product == null) return NotFound();
            return Ok(await EnrichProduct(product));
        }

        // ✅ ADMIN: Get ingredients for a menu item (enriched names)
        [HttpGet("admin/menu/{menuItemId}/ingredients")]
        public async Task<ActionResult<List<object>>> GetMenuItemIngredients(string menuItemId)
        {
            var product = await _products.Find(p => p.Id == menuItemId).FirstOrDefaultAsync();
            if (product == null || product.Ingredients == null)
                return Ok(new List<object>());

            var ids = product.Ingredients
                .Where(i => !string.IsNullOrEmpty(i.InventoryItemId))
                .Select(i => i.InventoryItemId)
                .ToList();

            var inventoryItems = ids.Any()
                ? await _inventoryItems.Find(i => ids.Contains(i.Id)).ToListAsync()
                : new List<InventoryItem>();

            var nameMap = inventoryItems.ToDictionary(i => i.Id, i => i.Name);

            var enriched = product.Ingredients.Select(i => new
            {
                inventoryItemId = i.InventoryItemId,
                name = nameMap.GetValueOrDefault(i.InventoryItemId, $"Unknown ({i.InventoryItemId})"),
                quantityRequired = i.QuantityRequired,
                unit = i.Unit
            }).ToList();

            return Ok(enriched);
        }

        // ✅ POST: Create new menu item
        [HttpPost("admin/menu")]
        public async Task<ActionResult<Product>> CreateMenuProduct([FromBody] ProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Product name is required.");

            var product = new Product
            {
                Name = dto.Name,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                Category = dto.Category,
                IsAvailable = dto.IsAvailable,
                ImageUrl = dto.ImageUrl?.Trim(),
                HasSizes = dto.HasSizes,
                Sizes = dto.Sizes ?? new List<string>(),
                HasMoods = dto.HasMoods,
                Moods = dto.Moods ?? new List<string>(),
                HasSugarLevels = dto.HasSugarLevels,
                SugarLevels = dto.SugarLevels ?? new List<int>(),
                Ingredients = dto.Ingredients ?? new List<MenuItemIngredient>()
            };

            await _products.InsertOneAsync(product);
            return CreatedAtAction(nameof(GetAdminProduct), new { id = product.Id }, product);
        }

        // ✅ PUT: Update existing menu item
        [HttpPut("admin/menu/{id}")]
        public async Task<IActionResult> UpdateMenuProduct(string id, [FromBody] UpdateProductDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Product name is required.");

            var existing = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (existing == null)
                return NotFound();

            var updated = new Product
            {
                Id = id,
                Name = dto.Name,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                Category = dto.Category,
                IsAvailable = dto.IsAvailable,
                ImageUrl = dto.ImageUrl?.Trim(),
                HasSizes = dto.HasSizes,
                Sizes = dto.Sizes ?? new List<string>(),
                HasMoods = dto.HasMoods,
                Moods = dto.Moods ?? new List<string>(),
                HasSugarLevels = dto.HasSugarLevels,
                SugarLevels = dto.SugarLevels ?? new List<int>(),
                Ingredients = dto.Ingredients ?? new List<MenuItemIngredient>()
            };

            await _products.ReplaceOneAsync(p => p.Id == id, updated);
            return NoContent();
        }

        // ✅ PUT: Update just the ingredients of a menu item
        [HttpPut("admin/menu/{menuItemId}/ingredients")]
        public async Task<IActionResult> UpdateMenuIngredients(string menuItemId, [FromBody] List<MenuItemIngredient> ingredients)
        {
            var product = await _products.Find(p => p.Id == menuItemId).FirstOrDefaultAsync();
            if (product == null)
                return NotFound();

            product.Ingredients = ingredients ?? new List<MenuItemIngredient>();
            await _products.ReplaceOneAsync(p => p.Id == menuItemId, product);
            return NoContent();
        }

        // ✅ Helper: Enrich one product
        private async Task<object> EnrichProduct(Product product)
        {
            var ids = product.Ingredients
                .Where(i => !string.IsNullOrEmpty(i.InventoryItemId))
                .Select(i => i.InventoryItemId)
                .ToList();

            var inventoryItems = ids.Any()
                ? await _inventoryItems.Find(i => ids.Contains(i.Id)).ToListAsync()
                : new List<InventoryItem>();

            var nameMap = inventoryItems.ToDictionary(i => i.Id, i => i.Name);

            var enrichedIngredients = product.Ingredients.Select(i => new
            {
                inventoryItemId = i.InventoryItemId,
                name = nameMap.GetValueOrDefault(i.InventoryItemId, $"Unknown ({i.InventoryItemId})"),
                quantityRequired = i.QuantityRequired,
                unit = i.Unit
            }).ToList();

            return new
            {
                product.Id,
                product.Name,
                product.Price,
                product.StockQuantity,
                product.Category,
                product.IsAvailable,
                product.ImageUrl,
                product.HasSizes,
                product.Sizes,
                product.HasMoods,
                product.Moods,
                product.HasSugarLevels,
                product.SugarLevels,
                Ingredients = enrichedIngredients
            };
        }

        // ✅ Helper: Enrich many products — with .Cast<object>() to fix List<anonymous> → List<object>
        private async Task<List<object>> EnrichProducts(List<Product> products)
        {
            var allIds = products
                .SelectMany(p => p.Ingredients)
                .Where(i => !string.IsNullOrEmpty(i.InventoryItemId))
                .Select(i => i.InventoryItemId)
                .Distinct()
                .ToList();

            var inventoryItems = allIds.Any()
                ? await _inventoryItems.Find(i => allIds.Contains(i.Id)).ToListAsync()
                : new List<InventoryItem>();

            var nameMap = inventoryItems.ToDictionary(i => i.Id, i => i.Name);

            return products.Select(p => new
            {
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                p.Category,
                p.IsAvailable,
                p.ImageUrl,
                p.HasSizes,
                p.Sizes,
                p.HasMoods,
                p.Moods,
                p.HasSugarLevels,
                p.SugarLevels,
                Ingredients = p.Ingredients.Select(i => new
                {
                    inventoryItemId = i.InventoryItemId,
                    name = nameMap.GetValueOrDefault(i.InventoryItemId, $"Unknown ({i.InventoryItemId})"),
                    quantityRequired = i.QuantityRequired,
                    unit = i.Unit
                }).ToList()
            })
            .Cast<object>()
            .ToList();
        }
    }
}