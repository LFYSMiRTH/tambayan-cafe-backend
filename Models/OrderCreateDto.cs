using System.Text.Json.Serialization;

namespace TambayanCafeAPI.Models
{
    public class OrderCreateDto
    {
        [JsonPropertyName("customerId")]
        public string? CustomerId { get; set; }

        [JsonPropertyName("customerEmail")]
        public string? CustomerEmail { get; set; }

        [JsonPropertyName("tableNumber")]
        public string? TableNumber { get; set; }

        [JsonPropertyName("items")]
        public List<OrderItemDto> Items { get; set; } = new();

        [JsonPropertyName("totalAmount")]
        public decimal TotalAmount { get; set; }

        [JsonPropertyName("placedByStaff")]
        public bool PlacedByStaff { get; set; }

        [JsonPropertyName("staffId")]
        public string? StaffId { get; set; }

        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }

        public string? DeliveryAddress { get; set; }
    }
}