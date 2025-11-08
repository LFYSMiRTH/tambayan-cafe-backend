using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class ProductWithAvailabilityDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public bool IsManuallyAvailable { get; set; }
        public List<MenuItemIngredient> Ingredients { get; set; } = new();
        public bool IsAvailable { get; set; }
        public string UnavailableReason { get; set; } = string.Empty;
    }
}