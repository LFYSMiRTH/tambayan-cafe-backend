namespace TambayanCafeAPI.Models
{
    public class ProductDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Category { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? ImageUrl { get; set; }
        public bool HasSizes { get; set; } = false;
        public List<string> Sizes { get; set; } = new List<string> { "S", "M", "L" };
        public bool HasMoods { get; set; } = false;
        public List<string> Moods { get; set; } = new List<string> { "Hot", "Ice" };
        public bool HasSugarLevels { get; set; } = false;
        public List<int> SugarLevels { get; set; } = new List<int> { 30, 50, 70 };
        public List<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
    }

    public class UpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Category { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? ImageUrl { get; set; }
        public bool HasSizes { get; set; } = false;
        public List<string> Sizes { get; set; } = new List<string> { "S", "M", "L" };
        public bool HasMoods { get; set; } = false;
        public List<string> Moods { get; set; } = new List<string> { "Hot", "Ice" };
        public bool HasSugarLevels { get; set; } = false;
        public List<int> SugarLevels { get; set; } = new List<int> { 30, 50, 70 };
        public List<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
    }
}