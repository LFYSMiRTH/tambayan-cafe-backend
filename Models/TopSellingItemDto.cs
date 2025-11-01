namespace TambayanCafeAPI.Models
{
    public class TopSellingItemDto
    {
        public string Name { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalRevenue { get; set; }
        public double? AvgRating { get; set; }
    }
}