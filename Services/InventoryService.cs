using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class InventoryService
    {
        private readonly IMongoCollection<InventoryItem> _inventory;

        public InventoryService(IMongoDatabase database)
        {
            _inventory = database.GetCollection<InventoryItem>("Inventory");
        }

        public List<InventoryItem> GetAll()
        {
            return _inventory.Find(_ => true).ToList();
        }
    }
}