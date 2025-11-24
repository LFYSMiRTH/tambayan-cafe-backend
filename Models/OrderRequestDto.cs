using System.ComponentModel.DataAnnotations;

namespace TambayanCafeAPI.Models
{
    public class OrderRequestDto
    {
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required]
        public List<OrderItemDto> Items { get; set; } = new();

        [Required]
        public decimal TotalAmount { get; set; }
    }

    public class OrderItemDto
    {
        [Required] public string ProductId { get; set; } = string.Empty;
        [Required] public string Name { get; set; } = string.Empty;
        [Required] public decimal Price { get; set; }
        [Required] public int Quantity { get; set; }
        public string Size { get; set; }
        public string Mood { get; set; }
        public string Sugar { get; set; }
    }
}