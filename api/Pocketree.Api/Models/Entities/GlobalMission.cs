using System.ComponentModel.DataAnnotations;
namespace ADproject.Models.Entities
{
    public class GlobalMission
    {
        [Key]
        public int MissionID { get; set; }
        [Required, StringLength(100)]
        public string MissionName { get; set; }
        [Required]
        public int TotalRequiredTrees { get; set; }
        public int CurrentTreeCount { get; set; }
        public int PlantingFrequency { get; set; } = 1; // Set to 1 initially for testing purpose
        
        // Navigation Properties
        public virtual ICollection<CommunityForest> PlantedTrees { get; set; }
        public virtual ICollection<Tree> Trees { get; set; }
    }
}
