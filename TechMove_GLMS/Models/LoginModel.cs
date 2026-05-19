// FILE: TechMove_GLMS/Models/LoginModel.cs
using System.ComponentModel.DataAnnotations;

namespace TechMove_GLMS.Models
{
    public class LoginModel
    {
        [Required]
        [EmailAddress]
        public required string Email { get; set; }
        [Required]
        public required string Password { get; set; }
    }
}