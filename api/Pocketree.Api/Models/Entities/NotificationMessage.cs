using ADproject.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pocketree.Api.Models.Entities
{
    public class NotificationMessage
    {
        [Key]
        public int MessageID { get; set; }
        public int AdminID { get; set; }
        [Required]
        public string? Message { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;

        // Navigation Property
        [ForeignKey("AdminID")]
        public virtual User? Admin { get; set; }
    }
}
