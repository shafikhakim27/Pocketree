using System.ComponentModel.DataAnnotations;

namespace ADproject.Models.DTOs
{
    public class ChangePasswordDto
    {
        [Required]
        public string CurrentPassword { get; set; }
        [Required, StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; }
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmNewPassword { get; set; }
    }
}
