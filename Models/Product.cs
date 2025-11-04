using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace TambayanCafeAPI.Models
{
    public class Product
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("price")]
        public decimal Price { get; set; }

        [BsonElement("stockQuantity")]
        public int StockQuantity { get; set; }

        [BsonElement("lowStockThreshold")]
        public int LowStockThreshold { get; set; } = 5;

        [BsonElement("category")]
        public string? Category { get; set; }

        [BsonElement("isAvailable")]
        public bool IsAvailable { get; set; } = true;

        [BsonElement("imageUrl")]
        public string? ImageUrl { get; set; }

        [BsonElement("ingredients")]
        public List<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
    }

    public class MenuItemIngredient
    {
        [BsonElement("inventoryItemId")]
        public string InventoryItemId { get; set; } = string.Empty;

        [BsonElement("quantityRequired")]
        public decimal QuantityRequired { get; set; } = 1;

        [BsonElement("unit")]
        public string Unit { get; set; } = "pcs";
    }
}