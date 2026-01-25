using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class UserSettings
    {
        [Key, ForeignKey("User")]
        public int UserID { get; set; }
        public bool EmailNotification { get; set; } = true;
        public bool UseMlRecommendation { get; set; } = false;
        public string Theme { get; set; } = "Light";
        // Navigation property
        public virtual User? User { get; set; } = null!;
    }
}
