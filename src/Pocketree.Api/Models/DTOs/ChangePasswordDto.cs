using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ADproject.Models.DTOs
{
    public class ChangePasswordDto
    {
        [Required]
        [JsonPropertyName("currentPassword")]
        public string CurrentPassword { get; set; }
        [Required, StringLength(100, MinimumLength = 8)]
        [JsonPropertyName("newPassword")]
        public string NewPassword { get; set; }
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        [JsonPropertyName("confirmNewPassword")]
        public string ConfirmNewPassword { get; set; }
    }
}
