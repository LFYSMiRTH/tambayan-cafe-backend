using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;
using System.Linq;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;
        private readonly SupplierService _supplierService;

        public AdminController(
            OrderService orderService,
            ProductService productService,
            InventoryService inventoryService,
            SupplierService supplierService)
        {
            _orderService = orderService;
            _productService = productService;
            _inventoryService = inventoryService;
            _supplierService = supplierService;
        }

        [HttpGet("dashboard")]
        public ActionResult<DashboardMetricsDto> GetDashboardMetrics()
        {
            return Ok(new DashboardMetricsDto
            {
                TotalOrders = (int)_orderService.GetTotalCount(),
                TotalRevenue = _orderService.GetTotalRevenue(),
                PendingOrders = (int)_orderService.GetPendingCount(),
                LowStockAlerts = (int)_productService.GetLowStockCount()
            });
        }

        [HttpGet("menu")]
        public ActionResult<List<Product>> GetAllMenuItems()
        {
            return Ok(_productService.GetAll());
        }

        [HttpPost("menu")]
        public ActionResult<Product> AddMenuItem([FromBody] Product item)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                return BadRequest("Item name is required.");
            if (item.Price < 0)
                return BadRequest("Price cannot be negative.");
            if (item.StockQuantity < 0)
                return BadRequest("Stock quantity cannot be negative.");

            _productService.Create(item);
            return Ok(item);
        }

        [HttpPut("menu/{id}")]
        public IActionResult UpdateMenuItem(string id, [FromBody] Product updatedItem)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");
            if (string.IsNullOrWhiteSpace(updatedItem.Name))
                return BadRequest("Name is required.");

            _productService.Update(id, updatedItem);
            return Ok();
        }

        [HttpDelete("menu/{id}")]
        public IActionResult DeleteMenuItem(string id)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");

            _productService.Delete(id);
            return Ok();
        }

        [HttpGet("inventory")]
        public ActionResult<List<InventoryItem>> GetInventory()
        {
            return Ok(_inventoryService.GetAll());
        }

        [HttpPost("inventory")]
        public IActionResult AddInventoryItem([FromBody] InventoryItem item)
        {
            if (string.IsNullOrWhiteSpace(item?.Name))
                return BadRequest("Ingredient name is required.");
            if (item.CurrentStock < 0 || item.ReorderLevel < 0)
                return BadRequest("Stock and reorder level must be non-negative.");

            _inventoryService.Create(item);
            return Ok(item);
        }

        [HttpGet("suppliers")]
        public ActionResult<List<Supplier>> GetSuppliers()
        {
            return Ok(_supplierService.GetAll());
        }

        [HttpPost("suppliers")]
        public ActionResult<Supplier> AddSupplier([FromBody] Supplier supplier)
        {
            if (string.IsNullOrWhiteSpace(supplier.Name) || string.IsNullOrWhiteSpace(supplier.Email))
                return BadRequest("Name and Email are required.");

            var created = _supplierService.Create(supplier);
            return Ok(created);
        }

        [HttpPut("suppliers/{id}")]
        public IActionResult UpdateSupplier(string id, [FromBody] Supplier supplier)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");
            if (string.IsNullOrWhiteSpace(supplier.Name) || string.IsNullOrWhiteSpace(supplier.Email))
                return BadRequest("Name and Email are required.");

            _supplierService.Update(id, supplier);
            return Ok();
        }

        [HttpGet("menu/{id}/ingredients")]
        public ActionResult<List<MenuItemIngredient>> GetMenuItemIngredients(string id)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");

            var product = _productService.GetAll().FirstOrDefault(p => p.Id == id);
            if (product == null)
                return NotFound();

            return Ok(product.Ingredients);
        }

        [HttpPut("menu/{id}/ingredients")]
        public IActionResult UpdateMenuItemIngredients(string id, [FromBody] List<MenuItemIngredient> ingredients)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");

            var product = _productService.GetAll().FirstOrDefault(p => p.Id == id);
            if (product == null)
                return NotFound();

            var inventoryItems = _inventoryService.GetAll();
            var validIds = inventoryItems.Select(i => i.Id).ToHashSet();
            foreach (var ing in ingredients)
            {
                if (string.IsNullOrWhiteSpace(ing.InventoryItemId) || !validIds.Contains(ing.InventoryItemId))
                    return BadRequest($"Invalid or missing inventory item ID: {ing.InventoryItemId}");
                if (ing.QuantityRequired <= 0)
                    return BadRequest("Quantity required must be greater than 0.");
            }

            product.Ingredients = ingredients;
            _productService.Update(id, product);
            return Ok(new { message = "Ingredients updated successfully" });
        }
    }
}