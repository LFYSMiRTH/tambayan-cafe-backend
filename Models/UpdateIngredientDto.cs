namespace TambayanCafeAPI.Models
{
    public class UpdateIngredientDto
    {
        public int InventoryItemId { get; set; }
        public decimal QuantityRequired { get; set; }
        public string Unit { get; set; } = "pcs";
    }
}