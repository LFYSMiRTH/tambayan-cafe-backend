// File: Models/OrderItem.cs (or wherever your OrderItem model is defined)
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class OrderItem
    {
        [BsonElement("productId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("priceAtOrder")] // Keep the existing field for storage
        public decimal PriceAtOrder { get; set; }

        // Add the Price property to match the JSON sent by JavaScript
        [BsonIgnore] // Don't store this in the DB, use PriceAtOrder instead
        public decimal Price
        {
            get => PriceAtOrder; // Read from PriceAtOrder
            set => PriceAtOrder = value; // Write to PriceAtOrder
        }

        // Add the new properties as optional fields (BsonIgnore if not stored, or BsonElement if stored)
        [BsonElement("name")] // Add name field
        public string Name { get; set; } = string.Empty;

        [BsonElement("size")] // Add size field
        public string Size { get; set; } = string.Empty;

        [BsonElement("mood")] // Add mood field
        public string Mood { get; set; } = string.Empty;

        [BsonElement("sugar")] // Add sugar field
        public string Sugar { get; set; } = string.Empty;
    }
}