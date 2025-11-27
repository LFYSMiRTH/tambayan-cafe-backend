using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class DatabaseSeeder
    {
        private readonly IMongoCollection<DeliveryZone> _deliveryZones;
        private readonly IConfiguration _config;
        private readonly ILogger<DatabaseSeeder> _logger;

        public DatabaseSeeder(IMongoDatabase database, IConfiguration config, ILogger<DatabaseSeeder> logger)
        {
            _deliveryZones = database.GetCollection<DeliveryZone>("DeliveryZones");
            _config = config;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                var count = await _deliveryZones.CountDocumentsAsync(_ => true);
                if (count == 0)
                {
                    _logger.LogInformation("DeliveryZones collection is empty. Seeding default zones...");

                    var zones = _config.GetSection("DeliveryZones")
                                       .Get<List<DeliveryZone>>() ?? new List<DeliveryZone>();

                    if (zones.Any())
                    {
                        await _deliveryZones.InsertManyAsync(zones);
                        _logger.LogInformation("Seeded {Count} delivery zones.", zones.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed DeliveryZones");
                throw;
            }
        }
    }
}