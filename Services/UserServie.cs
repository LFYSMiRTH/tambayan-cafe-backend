using MongoDB.Driver;
using TambayanCafeSystem.Models;

namespace TambayanCafeSystem.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;

        // ✅ Inject IMongoDatabase (from Program.cs)
        public UserService(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("users");
        }

        public List<User> Get() => _users.Find(user => true).ToList();

        public User Create(User user)
        {
            _users.InsertOne(user);
            return user;
        }

        public User GetByUsername(string username) =>
            _users.Find(user => user.Username == username).FirstOrDefault();
    }
}