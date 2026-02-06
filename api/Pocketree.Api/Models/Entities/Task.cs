using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace ADproject.Models.Entities
{
    public class Task
    {
        [Key]
        public int TaskID { get; set; }
        [Required]
        public string? Description { get; set; }
        [Required]
        public string? Difficulty { get; set; } = "Easy";
        [Required]
        public int CoinReward { get; set; } = 10;
        [Required]
        public bool RequiresEvidence { get; set; } = false; // For ML Verification
        [Required, StringLength(255)]
        public string? Keyword { get; set; } = ""; // For ML use
        [Required, StringLength(50)]
        public string? Category { get; set; } = "General"; // For ML use
        [Required, StringLength(255)]
        public string? NegativeKeyword { get; set; } = ""; // For ML use
        [Required]
        public string? SourceType { get; set; } = "Normal"; // For ML use

        [NotMapped]
        public bool isCompleted { get; set; } // Not created in DB, field is just to match Android's side
        [NotMapped]
        public bool isPassed {get; set;}
    }
}
