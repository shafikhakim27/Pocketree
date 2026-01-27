using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class UserBadge
    {
        [Key]
        public int UserBadgeID { get; set; }
        [Required]
        public int UserID { get; set; }
        [Required]
        public int BadgeID { get; set; }
        public DateTime DateEarned { get; set; }

        // Navigation Properties
        [ForeignKey("UserID")]
        public virtual User User { get; set; }
        [ForeignKey("BadgeID")]
        public virtual Badge Badge { get; set; }
    }
}
