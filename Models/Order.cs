using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace TambayanCafeAPI.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("customerId")]
        public string CustomerId { get; set; } = "000000000000000000000000";

        [BsonElement("customerEmail")]
        public string CustomerEmail { get; set; } = "walkin@tambayancafe.com";

        [BsonElement("customerName")]
        public string CustomerName { get; set; } = "Walk-in Customer";

        [BsonElement("tableNumber")]
        public string TableNumber { get; set; } = "N/A";

        [BsonElement("items")]
        public List<OrderItem> Items { get; set; } = new();

        [BsonElement("totalAmount")]
        public decimal TotalAmount { get; set; }

        [BsonElement("isCompleted")]
        public bool IsCompleted { get; set; } = false;

        [BsonElement("placedByStaff")]
        public bool PlacedByStaff { get; set; } = false;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("orderNumber")]
        public string OrderNumber { get; set; } = string.Empty;

        [BsonElement("status")]
        public string Status { get; set; } = "New";

        [BsonElement("paymentMethod")]
        public string PaymentMethod { get; set; } = "Cash";

        [BsonElement("deliveryFee")]
        public decimal DeliveryFee { get; set; }

        [BsonElement("deliveryAddress")]
        public string? DeliveryAddress { get; set; }
    }
}