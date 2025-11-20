using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TambayanCafeAPI.Services
{
    // The class already implements IUserService, which is good
    public class UserService : IUserService
    {
        private readonly IMongoCollection<User> _users;

        public UserService(IMongoDatabase database)
        {
            _users = database.GetCollection<User>("users");
        }

        public List<User> Get() => _users.Find(user => true).ToList();

        public User Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
                return null;
            return _users.Find(user => user.Id == id).FirstOrDefault();
        }

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
            return user.ResetCode == code && user.ResetCodeExpiry > DateTime.UtcNow;
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

        public void Update(string id, User updatedUser)
        {
            if (string.IsNullOrWhiteSpace(id) || updatedUser == null)
                return;

            if (!ObjectId.TryParse(id, out _))
                return;

            updatedUser.Id = id;
            _users.ReplaceOne(u => u.Id == id, updatedUser);
        }

        public void Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (!ObjectId.TryParse(id, out _))
                return;

            _users.DeleteOne(u => u.Id == id);
        }

        // Implement the method required by IUserService interface
        public async Task<User> GetUserByIdAsync(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !ObjectId.TryParse(id, out _))
                return null;
            return await _users.Find(user => user.Id == id).FirstOrDefaultAsync();
        }

        public async Task<User> GetUserProfileAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
                return null;
            return await _users.Find(user => user.Id == userId && user.DeletedAt == null).FirstOrDefaultAsync();
        }

        public async Task<bool> UpdateUserProfileAsync(string userId, User updatedUser)
        {
            if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
                return false;

            var filter = Builders<User>.Filter.And(
                Builders<User>.Filter.Eq(u => u.Id, userId),
                Builders<User>.Filter.Eq(u => u.DeletedAt, null)
            );

            var update = Builders<User>.Update
                .Set(u => u.FirstName, updatedUser.FirstName)
                .Set(u => u.LastName, updatedUser.LastName)
                .Set(u => u.Email, updatedUser.Email)
                .Set(u => u.PhoneNumber, updatedUser.PhoneNumber)
                .Set(u => u.Address, updatedUser.Address)
                .Set(u => u.Birthday, updatedUser.Birthday)
                .Set(u => u.Gender, updatedUser.Gender);

            var result = await _users.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
                return false;

            var user = await _users.Find(u => u.Id == userId && u.DeletedAt == null).FirstOrDefaultAsync();
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.Password))
                return false;

            var newHashedPassword = BCrypt.Net.BCrypt.HashPassword(newPassword);
            var update = Builders<User>.Update
                .Set(u => u.Password, newHashedPassword);

            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAccountAsync(string userId, string passwordConfirmation)
        {
            if (string.IsNullOrWhiteSpace(userId) || !ObjectId.TryParse(userId, out _))
                return false;

            var user = await _users.Find(u => u.Id == userId && u.DeletedAt == null).FirstOrDefaultAsync();
            if (user == null) return false;

            if (!BCrypt.Net.BCrypt.Verify(passwordConfirmation, user.Password))
                return false;

            var update = Builders<User>.Update
                .Set(u => u.DeletedAt, DateTime.UtcNow);

            var result = await _users.UpdateOneAsync(u => u.Id == userId, update);
            return result.ModifiedCount > 0;
        }
    }
}