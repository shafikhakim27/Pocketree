using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ADproject.Models.Entities
{
    public class Tree
    {
        [Key]
        public int TreeID { get; set; }
        [Required]
        public int UserID { get; set; }
        [Required]
        public int MissionID { get; set; }
        // Flag to indicate that the tree has contributed to the global mission once level 3 is reached
        public bool ContributeToMission { get; set; } = false;
        
        // Navigation Properties
        [ForeignKey("UserID")]
        public virtual User User { get; set; }
        [ForeignKey("MissionID")]
        public virtual GlobalMission GlobalMission { get; set; }
    }
}
