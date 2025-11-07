using System.Collections.Generic;

namespace TambayanCafeAPI.Models
{
    public class OrderRequestDto
    {
        public string CustomerId { get; set; }
        public string CustomerEmail { get; set; }
        public List<OrderItemDto> Items { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class OrderItemDto
    {
        public string ProductId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Size { get; set; }
        public string Mood { get; set; }
        public string Sugar { get; set; }
    }
}