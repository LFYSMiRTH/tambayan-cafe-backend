using MongoDB.Driver;
using TambayanCafeSystem.Models;

namespace TambayanCafeSystem.Services
{
    public class OrderService
    {
        private readonly IMongoCollection<Order> _orders;

        // ✅ Inject IMongoDatabase (from Program.cs)
        public OrderService(IMongoDatabase database)
        {
            _orders = database.GetCollection<Order>("orders");
        }

        public void Create(Order order)
        {
            _orders.InsertOne(order);
        }

        public List<Order> GetAll()
        {
            return _orders.Find(_ => true).ToList();
        }

        public long GetTotalCount()
        {
            return _orders.CountDocuments(_ => true);
        }

        public decimal GetTotalRevenue()
        {
            var orders = _orders.Find(_ => true).ToList();
            return orders.Sum(order => order.TotalAmount);
        }

        public long GetPendingCount()
        {
            return _orders.CountDocuments(order => !order.IsCompleted);
        }
    }
}