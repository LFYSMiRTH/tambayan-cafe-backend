namespace TambayanCafeAPI.Models
{
    public class DashboardMetricsDto
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public int PendingOrders { get; set; }
        public int LowStockAlerts { get; set; }
    }
}