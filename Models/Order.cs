using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } 

        [BsonElement("totalAmount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}