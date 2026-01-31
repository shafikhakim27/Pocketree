using System.ComponentModel.DataAnnotations;

namespace ADproject.Models.Entities
{
    public class Badge
    {
        [Key]
        public int BadgeID { get; set; }
        [Required, StringLength(50)]
        public string BadgeName { get; set; }
        [Required, StringLength(255)]
        public string Description { get; set; }
        public string BadgeImageURL { get; set; }
        [Required, StringLength(20)]
        public string CriteriaType { get; set; }
        [Required, StringLength(10)]
        public string RequiredDifficulty { get; set; }
        [Required]
        public int RequiredCount { get; set; }
    
        // Navigation Property
        public virtual ICollection<UserBadge> UserBadges { get; set; }
    }
}
