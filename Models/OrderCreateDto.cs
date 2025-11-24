using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TambayanCafeAPI.Models
{
    public class OrderCreateDto
    {
        public string? CustomerId { get; set; }

        public string? CustomerEmail { get; set; }

        [Required]
        public string CustomerName { get; set; } = "Walk-in Customer";

        public string? TableNumber { get; set; }

        [Required]
        public List<OrderItemDto> Items { get; set; } = new();

        [Required]
        public decimal TotalAmount { get; set; }

        public bool PlacedByStaff { get; set; }

        public string? StaffId { get; set; }
    }
}