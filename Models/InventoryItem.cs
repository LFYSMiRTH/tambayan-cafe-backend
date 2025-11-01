using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class InventoryItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;          
        public string Category { get; set; } = "General";
        public int CurrentStock { get; set; }                    
        public string Unit { get; set; } = "unit";
        public int ReorderLevel { get; set; } = 0;
    }
}