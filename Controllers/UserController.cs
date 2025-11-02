using Microsoft.AspNetCore.Mvc;
using TambayanCafeSystem.Services;
using System;
using System.Linq;
using System.Text.Json;
using TambayanCafeAPI.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text.RegularExpressions;

namespace TambayanCafeSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserService _userService;

        public UserController(UserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public ActionResult<List<User>> Get() => _userService.Get();

        [HttpGet("check-username")]
        public IActionResult CheckUsername([FromQuery] string username)
        {
            var user = _userService.GetByUsername(username);
            return Ok(new { exists = user != null });
        }

        [HttpGet("check-email")]
        public IActionResult CheckEmail([FromQuery] string email)
        {
            var user = _userService.GetByEmail(email);
            return Ok(new { exists = user != null });
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] User user)
        {
            if (!IsStrongPassword(user?.Password))
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

            // ✅ CRITICAL: Public signup = customer ONLY
            user.Id = null;
            user.Role = "customer"; // ← Enforced
            var createdUser = _userService.Create(user);
            return Ok(new { message = "User created successfully", user = createdUser });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] User loginUser)
        {
            var user = _userService.GetByUsername(loginUser.Username);
            if (user == null || user.Password != loginUser.Password)
            {
                return Unauthorized(new { error = "InvalidCredentials", message = "Invalid username or password" });
            }

            // ✅ Return role so frontend can redirect correctly
            return Ok(new
            {
                id = user.Id,
                username = user.Username,
                email = user.Email,
                role = user.Role // "customer" or "staff"
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] JsonElement request)
        {
            try
            {
                if (!request.TryGetProperty("email", out JsonElement emailElement) ||
                    string.IsNullOrWhiteSpace(emailElement.GetString()))
                {
                    return BadRequest(new { error = "MissingEmail", message = "Email is required." });
                }

                string email = emailElement.GetString().Trim();
                if (!IsValidEmail(email))
                {
                    return BadRequest(new { error = "InvalidEmail", message = "Please provide a valid email address." });
                }

                var user = _userService.GetByEmail(email);
                if (user == null)
                {
                    return Ok(new { message = "If your email is registered, a reset code was sent." });
                }

                string resetCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
                _userService.SaveResetCode(email, resetCode);

                Console.WriteLine($"[SENDGRID] Attempting to send email to: {email}");
                Console.WriteLine($"[SENDGRID] Using API Key: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SENDGRID_API_KEY")) ? "MISSING" : "PRESENT")}");

                var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        var client = new SendGrid.SendGridClient(apiKey);
                        var from = new SendGrid.Helpers.Mail.EmailAddress("johntimothyyanto@gmail.com", "TBYN Café");
                        var to = new SendGrid.Helpers.Mail.EmailAddress(email);
                        var subject = "Your Password Reset Code";
                        var plainTextContent = $"Your TBYN Café password reset code is: {resetCode}\n\nThis code expires in 10 minutes.";
                        var htmlContent = $"<p>Your TBYN Café password reset code is:</p><h2>{resetCode}</h2><p>This code expires in 10 minutes.</p>";
                        var msg = SendGrid.Helpers.Mail.MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

                        Console.WriteLine($"[SENDGRID] From: {from.Email} | To: {to.Email} | Subject: {subject}");

                        var response = await client.SendEmailAsync(msg);

                        Console.WriteLine($"[SENDGRID] SendGrid Response Status: {response.StatusCode}");
                        Console.WriteLine($"[SENDGRID] SendGrid Response Body: {await response.Body.ReadAsStringAsync()}");

                        Console.WriteLine("[SENDGRID] Email sent successfully!");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SENDGRID ERROR] {ex.GetType().Name}: {ex.Message}");
                        Console.WriteLine($"[SENDGRID ERROR] StackTrace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Console.WriteLine("[SENDGRID] SKIPPED: API key not found");
                }

                return Ok(new { message = "If your email is registered, a reset code was sent." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRITICAL] ForgotPassword failed: {ex}");
                return StatusCode(500, new { error = "Service temporarily unavailable" });
            }
        }

        [HttpPost("verify-reset-code")]
        public IActionResult VerifyResetCode([FromBody] JsonElement request)
        {
            if (!request.TryGetProperty("email", out JsonElement emailEl) ||
                !request.TryGetProperty("code", out JsonElement codeEl) ||
                string.IsNullOrWhiteSpace(emailEl.GetString()) ||
                string.IsNullOrWhiteSpace(codeEl.GetString()))
            {
                return BadRequest(new { error = "MissingFields", message = "Email and code are required." });
            }

            string email = emailEl.GetString().Trim();
            string code = codeEl.GetString().Trim();

            if (_userService.VerifyResetCode(email, code))
            {
                return Ok(new { message = "Code verified successfully" });
            }

            return BadRequest(new { error = "InvalidCode", message = "Invalid or expired code." });
        }

        [HttpPost("reset-password")]
        public IActionResult ResetPassword([FromBody] JsonElement request)
        {
            if (!request.TryGetProperty("email", out JsonElement emailEl) ||
                !request.TryGetProperty("code", out JsonElement codeEl) ||
                !request.TryGetProperty("newPassword", out JsonElement passEl) ||
                string.IsNullOrWhiteSpace(emailEl.GetString()) ||
                string.IsNullOrWhiteSpace(codeEl.GetString()) ||
                string.IsNullOrWhiteSpace(passEl.GetString()))
            {
                return BadRequest(new { error = "MissingFields", message = "Email, code, and new password are required." });
            }

            string email = emailEl.GetString().Trim();
            string code = codeEl.GetString().Trim();
            string newPassword = passEl.GetString();

            if (!IsStrongPassword(newPassword))
            {
                return BadRequest(new
                {
                    error = "WeakPassword",
                    message = "New password must be at least 8 characters and include uppercase, lowercase, number, and symbol."
                });
            }

            if (!_userService.VerifyResetCode(email, code))
            {
                return BadRequest(new { error = "InvalidCode", message = "Invalid or expired reset code." });
            }

            bool success = _userService.ResetPassword(email, newPassword);
            if (!success)
            {
                return NotFound(new { error = "UserNotFound", message = "User not found." });
            }

            return Ok(new { message = "Password has been reset successfully" });
        }

        // ✅ NEW ENDPOINT: Create Admin or Staff User (FULLY WORKING)
        [HttpPost("admin/users")]
        public async Task<IActionResult> CreateAdminOrStaffUser([FromBody] JsonElement request)
        {
            try
            {
                // Parse input
                if (!request.TryGetProperty("name", out JsonElement nameEl) ||
                    !request.TryGetProperty("email", out JsonElement emailEl) ||
                    !request.TryGetProperty("role", out JsonElement roleEl))
                {
                    return BadRequest(new { error = "MissingFields", message = "Name, email, and role are required." });
                }

                string fullName = nameEl.GetString()?.Trim();
                string email = emailEl.GetString()?.Trim();
                string role = roleEl.GetString()?.ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
                    return BadRequest(new { error = "InvalidInput", message = "Name and email cannot be empty." });

                if (role != "admin" && role != "staff")
                    return BadRequest(new { error = "InvalidRole", message = "Role must be 'admin' or 'staff'." });

                if (_userService.GetByEmail(email) != null)
                    return Conflict(new { error = "EmailExists", message = "A user with this email already exists." });

                // === Generate username from name ===
                string GenerateUsername(string name)
                {
                    var clean = Regex.Replace(name, @"[^a-zA-Z0-9\s]", "");
                    var parts = clean.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var first = parts.FirstOrDefault()?.ToLower() ?? "user";
                    var last = parts.Length > 1 ? parts.Last().ToLower() : "";
                    var baseName = (first.Substring(0, 1) + last);
                    if (string.IsNullOrEmpty(baseName)) baseName = "user";

                    int counter = 1;
                    string username = baseName;
                    while (_userService.GetByUsername(username) != null)
                    {
                        username = $"{baseName}{counter}";
                        counter++;
                    }
                    return username;
                }

                // === Generate strong password ===
                string GenerateStrongPassword()
                {
                    const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                    const string lower = "abcdefghijklmnopqrstuvwxyz";
                    const string digits = "0123456789";
                    const string symbols = "!@#$%^&*";
                    var all = upper + lower + digits + symbols;

                    var password = new char[12];
                    var rng = new Random();
                    password[0] = upper[rng.Next(upper.Length)];
                    password[1] = lower[rng.Next(lower.Length)];
                    password[2] = digits[rng.Next(digits.Length)];
                    password[3] = symbols[rng.Next(symbols.Length)];

                    for (int i = 4; i < 12; i++)
                        password[i] = all[rng.Next(all.Length)];

                    return new string(password.OrderBy(_ => Guid.NewGuid()).ToArray());
                }

                var username = GenerateUsername(fullName);
                var password = GenerateStrongPassword();

                // Create user object — matches your User model
                var newUser = new User
                {
                    Username = username,
                    Email = email,
                    Password = password, // plain-text (your current design)
                    Role = role
                };

                // Save to DB
                var createdUser = _userService.Create(newUser);

                // === SEND WELCOME EMAIL ===
                var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    try
                    {
                        var client = new SendGridClient(apiKey);
                        var from = new EmailAddress("johntimothyyanto@gmail.com", "TBYN Café Admin");
                        var to = new EmailAddress(email);
                        var subject = "Welcome to TBYN Café – Your Account is Ready!";
                        var htmlContent = $@"
                            <p>Hi <strong>{fullName}</strong>,</p>
                            <p>You’ve been added as a <strong>{role}</strong> to TBYN Café.</p>
                            <p><strong>Login Details:</strong></p>
                            <ul>
                                <li><strong>Username:</strong> {username}</li>
                                <li><strong>Temporary Password:</strong> {password}</li>
                            </ul>
                            <p>Please log in and change your password immediately:</p>
                            <p><a href='https://my-frontend-app-eight.vercel.app/login' style='color:#2ECC71;'>Go to Login</a></p>
                            <p style='font-size:0.8em; color:#777;'>This password will expire after first use.</p>
                        ";
                        var msg = MailHelper.CreateSingleEmail(from, to, subject, "", htmlContent);
                        await client.SendEmailAsync(msg);
                        Console.WriteLine($"[SENDGRID] Welcome email sent to {email}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SENDGRID ERROR] Failed to send welcome email: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("[SENDGRID] API key missing — skipping email");
                }

                return CreatedAtAction(nameof(Get), new { id = createdUser.Id }, new
                {
                    id = createdUser.Id,
                    username = createdUser.Username,
                    email = createdUser.Email,
                    role = createdUser.Role
                });

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CreateAdminOrStaffUser failed: {ex}");
                return StatusCode(500, new { error = "Failed to create user" });
            }
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

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}