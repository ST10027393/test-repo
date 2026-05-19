using System.ComponentModel.DataAnnotations;

namespace TechMove_GLMS.Models;

public class RegisterModel
{
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    [Required]
    public required string Password { get; set; }
    
    [Required]
    public required string FirstName { get; set; }

    [Required]
    public required string Surname { get; set; }
    
    [Required]
    public required string Role { get; set; } // Bound to a dropdown in the View (Admin or Logistics Manager)
}