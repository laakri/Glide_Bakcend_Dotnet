using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace B2CPlatform.Models
{
    public enum ReportStatus
    {
        Pending,
        Reviewed,
        Resolved
    }

    public class Report
    {
        public int Id { get; set; }
        [Required]
        public required string UserId { get; set; }
        public User User { get; set; }
        [Required]
        public int OrderId { get; set; }
        public Order Order { get; set; }
        [Required]
        public required string Description { get; set; }
        [Required]
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
