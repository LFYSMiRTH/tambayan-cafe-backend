using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Controllers
{
    [ApiController]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly IMongoDatabase _database;

        public DevController(IMongoDatabase database)
        {
            _database = database;
        }

        [HttpPost("hash-passwords")]
        public async Task<IActionResult> HashAllPasswords()
        {
            var users = _database.GetCollection<User>("users");
            var all = await users.Find(_ => true).ToListAsync();

            foreach (var user in all)
            {
                // Skip if already hashed
                if (user.Password.StartsWith("$2"))
                    continue;

                // Hash plaintext password
                string hashed = BCrypt.Net.BCrypt.HashPassword(user.Password);
                await users.UpdateOneAsync(
                    u => u.Id == user.Id,
                    Builders<User>.Update.Set(u => u.Password, hashed)
                );
            }

            return Ok("All passwords hashed.");
        }
    }
}