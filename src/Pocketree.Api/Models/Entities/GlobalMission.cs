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
    }
}
