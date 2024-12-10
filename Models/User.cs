using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using B2CPlatform.Models;

public enum UserRole
{
    Client,
    Admin,
    Delivery
}

public class User
{
    public string? Id { get; set; }

    public required string Username { get; set; }

    public required string Password { get; set; }

    [EmailAddress]
    public required string Email { get; set; }

    public string? GoogleId { get; set; }

    public UserRole Role { get; set; } = UserRole.Client; 

    public ICollection<Order>? Orders { get; set; }
    public ICollection<Report>? Reports { get; set; } 
    public ICollection<Rating>? Ratings { get; set; } 

}
