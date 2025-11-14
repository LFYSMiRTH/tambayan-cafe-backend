using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using TambayanCafeSystem.Services;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")] // ✅ Global auth handles this
    public class AdminController : ControllerBase
    {
        private readonly OrderService _orderService;
        private readonly ProductService _productService;
        private readonly InventoryService _inventoryService;
        private readonly SupplierService _supplierService;
        private readonly UserService _userService;

        public AdminController(
            OrderService orderService,
            ProductService productService,
            InventoryService inventoryService,
            SupplierService supplierService,
            UserService userService)
        {
            _orderService = orderService;
            _productService = productService;
            _inventoryService = inventoryService;
            _supplierService = supplierService;
            _userService = userService;
        }

        // ✅ REMOVED IsAdmin() method

        [HttpGet("dashboard")]
        public ActionResult<DashboardMetricsDto> GetDashboardMetrics()
        {
            // ✅ NO MANUAL CHECK NEEDED - [Authorize] handles it
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

        [HttpGet("users")]
        public ActionResult<List<User>> GetAllUsers()
        {
            return Ok(_userService.Get());
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] User user)
        {
            if (user == null)
                return BadRequest("User data is required.");

            if (!IsStrongPassword(user.Password))
            {
                return BadRequest(new
                {
                    error = "WeakPassword",
                    message = "Password must be at least 8 characters and include uppercase, lowercase, number, and symbol."
                });
            }

            if (_userService.GetByUsername(user.Username) != null)
            {
                return Conflict(new { error = "UsernameExists", message = "Username already taken." });
            }

            if (_userService.GetByEmail(user.Email) != null)
            {
                return Conflict(new { error = "EmailExists", message = "Email already registered." });
            }

            // ✅ HASH PASSWORD BEFORE SAVING
            user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            user.Id = null;
            var createdUser = _userService.Create(user);

            // ✅ ADD WELCOME EMAIL LOGIC HERE
            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    var client = new SendGrid.SendGridClient(apiKey);
                    var from = new SendGrid.Helpers.Mail.EmailAddress("johntimothyyanto@gmail.com", "TBYN Café Admin");
                    var to = new SendGrid.Helpers.Mail.EmailAddress(createdUser.Email);
                    var subject = "Welcome to TBYN Café – Your Account is Ready!";
                    var htmlContent = $@"
                        <p>Hi <strong>{System.Net.WebUtility.HtmlEncode(createdUser.Name ?? createdUser.Username)}</strong>,</p>
                        <p>You've been added as a <strong>{System.Net.WebUtility.HtmlEncode(createdUser.Role)}</strong> to TBYN Café.</p>
                        <p><strong>Login Details:</strong></p>
                        <ul>
                            <li><strong>Username:</strong> {System.Net.WebUtility.HtmlEncode(createdUser.Username)}</li>
                            <li><strong>Password:</strong> (as provided during creation)</li>
                        </ul>
                        <p>Please log in and change your password if needed:</p>
                        <p><a href='https://my-frontend-app-eight.vercel.app/login' style='color:#2ECC71;'>Go to Login</a></p>
                    ";
                    var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
                    await client.SendEmailAsync(msg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SENDGRID] Failed to send welcome email: {ex.Message}");
                }
            }

            return Ok(createdUser);
        }

        [HttpGet("customers")]
        public ActionResult<List<User>> GetAllCustomers()
        {
            return Ok(_userService.Get());
        }

        [HttpPut("users/{id}")]
        public IActionResult UpdateUser(string id, [FromBody] UpdateUserDto updateDto)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid user ID format.");

            var existingUser = _userService.Get(id);
            if (existingUser == null)
                return NotFound("User not found.");

            existingUser.Name = !string.IsNullOrWhiteSpace(updateDto.Name) ? updateDto.Name.Trim() : existingUser.Name;
            existingUser.Email = !string.IsNullOrWhiteSpace(updateDto.Email) ? updateDto.Email.Trim() : existingUser.Email;

            if (updateDto.Role == "admin" || updateDto.Role == "staff")
                existingUser.Role = updateDto.Role;

            existingUser.IsActive = updateDto.IsActive;

            _userService.Update(id, existingUser);
            return Ok(existingUser);
        }

        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(string id)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid user ID format.");

            var user = _userService.Get(id);
            if (user == null)
                return NotFound("User not found.");

            _userService.Remove(id);
            return Ok(new { message = "User deleted successfully." });
        }

        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(string id)
        {
            var user = _userService.Get(id);
            if (user == null)
                return NotFound("User not found.");

            string GenerateStrongPassword()
            {
                const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                const string lower = "abcdefghijklmnopqrstuvwxyz";
                const string digits = "0123456789";
                const string symbols = "!@#$%^&*";
                var all = upper + lower + digits + symbols;
                var rng = new Random();
                var password = new char[12];
                password[0] = upper[rng.Next(upper.Length)];
                password[1] = lower[rng.Next(lower.Length)];
                password[2] = digits[rng.Next(digits.Length)];
                password[3] = symbols[rng.Next(symbols.Length)];
                for (int i = 4; i < 12; i++)
                    password[i] = all[rng.Next(all.Length)];
                return new string(password.OrderBy(_ => Guid.NewGuid()).ToArray());
            }

            var newPassword = GenerateStrongPassword();
            user.Password = newPassword;
            _userService.Update(id, user);

            var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
            if (!string.IsNullOrEmpty(apiKey))
            {
                try
                {
                    var client = new SendGrid.SendGridClient(apiKey);
                    var from = new SendGrid.Helpers.Mail.EmailAddress("johntimothyyanto@gmail.com", "TBYN Café Admin");
                    var to = new SendGrid.Helpers.Mail.EmailAddress(user.Email);
                    var subject = "Your Password Has Been Reset";
                    var htmlContent = $@"
                        <p>Hello {user.Username},</p>
                        <p>An administrator has reset your password.</p>
                        <p><strong>New temporary password:</strong> {newPassword}</p>
                        <p>Please log in and change it immediately.</p>
                        <p><a href='https://my-frontend-app-eight.vercel.app/login' style='color:#2ECC71;'>Go to Login</a></p>";
                    var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
                    await client.SendEmailAsync(msg);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SENDGRID] Failed to send password reset email: {ex.Message}");
                }
            }

            return Ok(new { message = "Password reset email sent." });
        }

        [HttpPut("customers/{id}/status")]
        public IActionResult UpdateCustomerStatus(string id, [FromBody] CustomerStatusDto statusDto)
        {
            if (!ObjectId.TryParse(id, out _))
                return BadRequest("Invalid customer ID.");

            var customer = _userService.Get(id);
            if (customer == null)
                return NotFound("Customer not found.");

            if (customer.Role != "customer")
                return BadRequest("Only customers can be blocked/unblocked.");

            customer.IsActive = statusDto.IsActive;
            _userService.Update(id, customer);
            return Ok(new { message = $"Customer is now {(statusDto.IsActive ? "active" : "blocked")}." });
        }

        private bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
                return false;

            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }
    }

    public class UpdateUserDto
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool IsActive { get; set; }
    }

    public class CustomerStatusDto
    {
        public bool IsActive { get; set; }
    }
}