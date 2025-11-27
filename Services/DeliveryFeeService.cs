using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class DeliveryFeeService : IDeliveryFeeService
    {
        private readonly IMongoCollection<DeliveryZone> _deliveryZones;

        public DeliveryFeeService(IMongoDatabase database)
        {
            _deliveryZones = database.GetCollection<DeliveryZone>("DeliveryZones");
        }

        public async Task<decimal> CalculateDeliveryFeeAsync(string fullAddress)
        {
            if (string.IsNullOrWhiteSpace(fullAddress))
                return 0;

            var activeZones = await _deliveryZones
                .Find(z => z.IsActive)
                .ToListAsync();

            foreach (var zone in activeZones)
            {
                if (fullAddress.Contains(zone.CityOrArea, StringComparison.OrdinalIgnoreCase))
                {
                    return zone.Fee;
                }
            }

            return 80.00m; // Default out-of-coverage fee
        }
    }
}