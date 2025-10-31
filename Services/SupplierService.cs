using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeSystem.Services
{
    public class SupplierService
    {
        private readonly IMongoCollection<Supplier> _suppliers;

        public SupplierService(IMongoDatabase database)
        {
            _suppliers = database.GetCollection<Supplier>("Suppliers");
        }

        public List<Supplier> GetAll()
        {
            return _suppliers.Find(_ => true).ToList();
        }

        public Supplier Create(Supplier supplier)
        {
            // MongoDB auto-generates ObjectId if Id is empty
            if (string.IsNullOrEmpty(supplier.Id))
            {
                supplier.Id = null; // Let MongoDB assign it
            }
            _suppliers.InsertOne(supplier);
            return supplier;
        }

        public void Update(string id, Supplier supplier)
        {
            var filter = Builders<Supplier>.Filter.Eq(s => s.Id, id);
            _suppliers.ReplaceOne(filter, supplier);
        }
    }
}