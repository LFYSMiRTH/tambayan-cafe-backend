using Microsoft.AspNetCore.Mvc;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;

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
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");
            if (string.IsNullOrWhiteSpace(updatedItem.Name))
                return BadRequest("Name is required.");

            _productService.Update(id, updatedItem);
            return Ok();
        }

        [HttpDelete("menu/{id}")]
        public IActionResult DeleteMenuItem(string id)
        {
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");

            _productService.Delete(id);
            return Ok();
        }

        // =============================
        // 🆕 INVENTORY MANAGEMENT
        // =============================

        [HttpGet("inventory")]
        public ActionResult<List<InventoryItem>> GetInventory()
        {
            return Ok(_inventoryService.GetAll());
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
            if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
                return BadRequest("Invalid ID format.");
            if (string.IsNullOrWhiteSpace(supplier.Name) || string.IsNullOrWhiteSpace(supplier.Email))
                return BadRequest("Name and Email are required.");

            _supplierService.Update(id, supplier);
            return Ok();
        }
    }
}