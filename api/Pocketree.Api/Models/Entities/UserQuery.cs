using ADproject.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Pocketree.Api.Models.Entities
{
    public class UserQuery
    {
        [Key]
        public int QueryID { get; set; }
        [Required]
        public int UserID { get; set; }
        [Required, StringLength(255)]
        public string? Query { get; set; } = null;
        [StringLength(255)]
        public string? AdminReply { get; set; } = null;
        [Required]
        public bool IsResolved { get; set; } = false;
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        // Navigation Property
        [ForeignKey("UserID")]
        public virtual User? User { get; set; }
    }
}
