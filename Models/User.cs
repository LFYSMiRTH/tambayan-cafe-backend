using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace TambayanCafeAPI.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]           
        public string? Name { get; set; } 

        [BsonElement("username")]
        public string Username { get; set; }

        [BsonElement("email")]
        public string? Email { get; set; }

        [BsonElement("password")]
        public string Password { get; set; }

        [BsonElement("role")]
        public string Role { get; set; } = "customer";

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("resetCode")]
        public string? ResetCode { get; set; }

        [BsonElement("resetCodeExpiry")]
        public DateTime? ResetCodeExpiry { get; set; }
    }
}