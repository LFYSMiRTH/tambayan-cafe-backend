using MongoDB.Driver;
using TambayanCafeAPI.Models;
using System;
using System.Collections.Generic;

namespace TambayanCafeSystem.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;
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

        public User GetByEmail(string email) =>
            _users.Find(user => user.Email == email).FirstOrDefault();

        public void SaveResetCode(string email, string code)
        {
            var update = Builders<User>.Update
                .Set(u => u.ResetCode, code)
                .Set(u => u.ResetCodeExpiry, DateTime.UtcNow.AddMinutes(10)); 

            _users.UpdateOne(u => u.Email == email, update);
        }

        public bool VerifyResetCode(string email, string code)
        {
            var user = _users.Find(u => u.Email == email).FirstOrDefault();
            if (user == null) return false;

            if (user.ResetCode == code && user.ResetCodeExpiry > DateTime.UtcNow)
                return true;

            return false;
        }

        public bool ResetPassword(string email, string newPassword)
        {
            var user = _users.Find(u => u.Email == email).FirstOrDefault();
            if (user == null) return false;

            var update = Builders<User>.Update
                .Set(u => u.Password, newPassword)
                .Unset(u => u.ResetCode)
                .Unset(u => u.ResetCodeExpiry);

            _users.UpdateOne(u => u.Email == email, update);
            return true;
        }
    }
}
