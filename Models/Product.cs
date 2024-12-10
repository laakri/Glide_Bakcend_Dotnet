namespace B2CPlatform.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string ShortDescription { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int CategoryId { get; set; }
        public Category? Category { get; set; }
        public List<string> Colors { get; set; } = new List<string>();
        public List<string> Sizes { get; set; } = new List<string>();
        public List<Rating> Ratings { get; set; } = new List<Rating>();

        public Product()
        {
            Name = "";
            Description = "";
            ImageUrl = "";
            ShortDescription = "";
        }
    }
}
