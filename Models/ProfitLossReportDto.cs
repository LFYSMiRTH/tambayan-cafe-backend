namespace TambayanCafeAPI.Models
{
    public class ProfitLossReportDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; } 
        public decimal NetProfit => TotalRevenue - TotalExpenses;
        public double ProfitMargin => TotalRevenue == 0 ? 0 : (double)(NetProfit / TotalRevenue);
    }
}