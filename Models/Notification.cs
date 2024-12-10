using System.ComponentModel.DataAnnotations;
using B2CPlatform.Models;

    public class Notification
    {
        public int? Id { get; set; }

        public required string UserId { get; set; }  

        public required string Title { get; set; }   

        public required string Message { get; set; } 

        public bool IsRead { get; set; } = false;  

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
    }
