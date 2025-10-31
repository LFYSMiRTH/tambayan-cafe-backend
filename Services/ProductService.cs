using MongoDB.Driver;
using TambayanCafeAPI.Models;
using MongoDB.Bson;

namespace TambayanCafeSystem.Services
{
    public class ProductService
    {
        private readonly IMongoCollection<Product> _products;

        public ProductService(IMongoDatabase database)
        {
            _products = database.GetCollection<Product>("products");
        }

        public void Create(Product product) => _products.InsertOne(product);

        public List<Product> GetAll() => _products.Find(_ => true).ToList();

        public void Update(string id, Product product)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid product ID format.", nameof(id));

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            var update = Builders<Product>.Update
                .Set("name", product.Name)
                .Set("price", product.Price)
                .Set("stockQuantity", product.StockQuantity)
                .Set("category", product.Category ?? ""); // prevent null

            _products.UpdateOne(filter, update);
        }

        public void Delete(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                throw new ArgumentException("Invalid product ID format.", nameof(id));

            var filter = Builders<Product>.Filter.Eq("_id", objectId);
            _products.DeleteOne(filter);
        }

        public long GetLowStockCount() =>
            _products.CountDocuments(p => p.StockQuantity <= (p.LowStockThreshold > 0 ? p.LowStockThreshold : 5));
    }
}