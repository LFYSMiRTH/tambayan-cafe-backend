using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using TambayanCafeAPI.Models;

namespace TambayanCafeAPI.Services
{
    public class NotificationService
    {
        private readonly IMongoCollection<Notification> _notifications;

        public NotificationService(IMongoDatabase database)
        {
            _notifications = database.GetCollection<Notification>("notifications");
        }

        public async Task CreateAsync(Notification notification)
        {
            if (notification == null)
                throw new ArgumentNullException(nameof(notification));

            await _notifications.InsertOneAsync(notification);
        }

        public async Task<List<Notification>> GetUnreadAsync()
        {
            return await _notifications
                .Find(n => !n.IsRead)
                .SortByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<long> GetUnreadCountAsync()
        {
            return await _notifications.CountDocumentsAsync(n => !n.IsRead);
        }

        public async Task MarkAsReadAsync(string id)
        {
            var filter = Builders<Notification>.Filter.Eq("_id", ObjectId.Parse(id));
            var update = Builders<Notification>.Update.Set(n => n.IsRead, true);

            await _notifications.UpdateOneAsync(filter, update);
        }

        public async Task<List<Notification>> GetAllAsync(int limit = 10)
        {
            return await _notifications
                .Find(_ => true)
                .SortByDescending(n => n.CreatedAt)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetNotificationsForRoleAsync(string role, int limit = 10)
        {
            var filter = Builders<Notification>.Filter.Eq(n => n.TargetRole, role);
            var sort = Builders<Notification>.Sort.Descending(n => n.CreatedAt);
            return await _notifications.Find(filter).Sort(sort).Limit(limit).ToListAsync();
        }

        // ✅ ADD THIS METHOD
        public async Task<List<Notification>> GetNotificationsForCustomerAsync(string customerId, int limit = 10)
        {
            var filter = Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.CustomerId, customerId),
                Builders<Notification>.Filter.Ne(n => n.TargetRole, "staff") // Exclude staff-only notifications
            );
            var sort = Builders<Notification>.Sort.Descending(n => n.CreatedAt);
            return await _notifications.Find(filter).Sort(sort).Limit(limit).ToListAsync();
        }

        public async Task CreateNotificationAsync(Notification notification)
        {
            await CreateAsync(notification);
        }
    }
}