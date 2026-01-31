using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class UserPreference
    {
        [Key]
        public int PreferenceID { get; set; }
        public int UserID { get; set; }
        [Required, StringLength(50)]
        public string PreferredCategory { get; set; } = "General";
        [Required, StringLength(20)]
        public string PreferredDifficulty { get; set; } = "Easy";

        // Navigation property
        [ForeignKey("UserID")]
        public virtual User User { get; set; } = null!;
    }
}
