using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TambayanCafeAPI.Models
{
    public class Notification
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("type")]
        public string Type { get; set; } = "info"; 

        [BsonElement("isRead")]
        public bool IsRead { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("relatedId")]
        public string RelatedId { get; set; } = string.Empty;

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("targetRole")]
        public string TargetRole { get; set; } = "staff"; 
    }
}