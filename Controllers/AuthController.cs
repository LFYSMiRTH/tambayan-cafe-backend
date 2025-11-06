using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using TambayanCafeAPI.Models;
using TambayanCafeAPI.Services;
using Tambrypt = BCrypt.Net.BCrypt;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly IConfiguration _configuration;

        public AuthController(UserService userService, IConfiguration configuration)
        {
            _userService = userService;
            _configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
        {
            if (string.IsNullOrWhiteSpace(loginDto.Username) || string.IsNullOrWhiteSpace(loginDto.Password))
                return BadRequest(new { error = "Username and password are required." });

            var user = _userService.GetByUsername(loginDto.Username);
            if (user == null)
                return Unauthorized(new { error = "Invalid credentials." });

            if (!user.IsActive)
                return Unauthorized(new { error = "Account is inactive or blocked." });

            // ✅ VERIFY AGAINST HASHED PASSWORD
            if (!Tambrypt.Verify(loginDto.Password, user.Password))
                return Unauthorized(new { error = "Invalid credentials." });

            // ✅ LOAD JWT CONFIG
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
                         ?? _configuration["Jwt:Key"]
                         ?? "ThisIsYourVerySecureSecretKey123!@#";

            var issuer = _configuration["Jwt:Issuer"] ?? "TambayanCafeAPI";
            var audience = _configuration["Jwt:Audience"] ?? "TambayanCafeClient";

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtKey);

            // ✅ IMPORTANT FIX: USE ClaimTypes.Role
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id), // ✅ Standard nameidentifier
                new Claim(ClaimTypes.Role, user.Role.ToLowerInvariant()) // ✅ Standard role
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new
            {
                token = tokenString,
                user = new
                {
                    user.Id,
                    user.Username,
                    user.Name,
                    user.Email,
                    Role = user.Role.ToLowerInvariant()
                }
            });
        }
    }

    public class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
