namespace TambayanCafeAPI.Models
{
    public class CustomerInsightsDto
    {
        public int NewCustomers { get; set; }
        public int RepeatCustomers { get; set; }
        public double RetentionRate { get; set; }
    }
}