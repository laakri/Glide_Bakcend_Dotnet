namespace B2CPlatform.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category>? SubCategories { get; set; }
        public ICollection<Product>? Products { get; set; }

        public Category()
        {
            Name = "";
            SubCategories = new List<Category>();
            Products = new List<Product>();
        }
    }
}
