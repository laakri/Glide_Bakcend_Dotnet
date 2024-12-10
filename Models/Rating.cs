namespace B2CPlatform.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public required string UserId { get; set; }
        public User User { get; set; }       
         public int Score { get; set; } // Score from 1 to 5
        public string Comment { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
