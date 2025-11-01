// Models/Order.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TambayanCafeAPI.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("customerEmail")]
        public string CustomerEmail { get; set; } = string.Empty;

        [BsonElement("items")]
        public List<OrderItem> Items { get; set; } = new();

        [BsonElement("totalAmount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonIgnore] 
        public string Status => IsCompleted ? "Completed" : "Pending";
    }
}