namespace TambayanCafeAPI.Models
{
    public class ExpenseDto
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
    }
}