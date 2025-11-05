using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using Tambrypt = BCrypt.Net.BCrypt; // 👈 ALIAS FOR BCrypt

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
            var usersCollection = _database.GetCollection<User>("users");
            var users = await usersCollection.Find(_ => true).ToListAsync();

            foreach (var user in users)
            {
                // Skip if already hashed (BCrypt hashes start with $2a$, $2b$, or $2y$)
                if (user.Password.StartsWith("$2"))
                    continue;

                // ✅ Correct way to hash
                string hashedPassword = Tambrypt.HashPassword(user.Password);
                var update = Builders<User>.Update.Set(u => u.Password, hashedPassword);
                await usersCollection.UpdateOneAsync(u => u.Id == user.Id, update);
            }

            return Ok(new { message = "All plaintext passwords have been hashed with BCrypt." });
        }
    }
}