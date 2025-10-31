using MongoDB.Bson.Serialization.Attributes;

namespace TambayanCafeAPI.Models
{
    public class OrderItem
    {
        [BsonElement("productId")]
        public string ProductId { get; set; } = string.Empty;

        [BsonElement("quantity")]
        public int Quantity { get; set; }

        [BsonElement("priceAtOrder")]
        public decimal PriceAtOrder { get; set; }
    }
}