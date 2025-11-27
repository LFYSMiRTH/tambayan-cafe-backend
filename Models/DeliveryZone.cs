using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class DeliveryZone
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("cityOrArea")]
        public string CityOrArea { get; set; } = string.Empty;

        [BsonElement("fee")]
        public decimal Fee { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
    }
}