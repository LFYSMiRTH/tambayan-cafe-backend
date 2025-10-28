using MongoDB.Driver;
using MongoDB.Bson;
using TambayanCafeSystem.Models;

namespace TambayanCafeSystem.Services
{
    public class ProductService
    {
        private readonly IMongoCollection<Product> _products;

        // Inject IMongoDatabase (configured in Program.cs)
        public ProductService(IMongoDatabase database)
        {
            _products = database.GetCollection<Product>("products");
        }

        public void Create(Product product) => _products.InsertOne(product);

        public List<Product> GetAll() => _products.Find(_ => true).ToList();

        public void Update(string id, Product product)
        {
            var filter = Builders<Product>.Filter.Eq("_id", ObjectId.Parse(id));
            var update = Builders<Product>.Update
                .Set("name", product.Name)
                .Set("price", product.Price)
                .Set("stockQuantity", product.StockQuantity)
                .Set("category", product.Category)
                .Set("lowStockThreshold", product.LowStockThreshold);
            _products.UpdateOne(filter, update);
        }

        public void Delete(string id)
        {
            var filter = Builders<Product>.Filter.Eq("_id", ObjectId.Parse(id));
            _products.DeleteOne(filter);
        }

        public long GetLowStockCount() =>
            _products.CountDocuments(p => p.StockQuantity <= p.LowStockThreshold);
    }
}