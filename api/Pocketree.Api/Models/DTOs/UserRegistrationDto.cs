using System.ComponentModel.DataAnnotations;

namespace ADproject.Models.DTOs
{
    public class UserRegistrationDto
    {
        [Required, StringLength(50)]
        public string Username { get; set; }
        [Required, StringLength(100, MinimumLength = 8)]
        public string Password { get; set; }
        [Required, StringLength(50)]
        public string Email { get; set; }
    }
}
