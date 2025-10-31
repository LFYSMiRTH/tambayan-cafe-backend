// File: Models/InventoryItem.cs
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models  // 👈 MUST match your project name
{
    public class InventoryItem
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? Unit { get; set; } = "pcs";
        public int CurrentStock { get; set; } = 0;
        public int ReorderLevel { get; set; } = 10;
    }
}