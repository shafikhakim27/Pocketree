using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class CommunityForest
    {
        [Key]
        public int ForestTreeID { get; set; }
        [Required]
        public double XCoordinate { get; set; }
        [Required]
        public double YCoordinate { get; set; }
        [Required]
        public int MissionID { get; set; }
        public DateTime PlantedAt { get; set; } = DateTime.UtcNow;
        // Navigation Property
        [ForeignKey("MissionID")]
        public virtual GlobalMission Mission { get; set; }
    }
}
