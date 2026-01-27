using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ADproject.Models.DTOs
{
    public class UserLoginDto
    {
        [Required]
        [JsonPropertyName("username")]
        public string Username { get; set; }
        [Required]
        [JsonPropertyName("password")]
        public string Password { get; set; }
    }
}
