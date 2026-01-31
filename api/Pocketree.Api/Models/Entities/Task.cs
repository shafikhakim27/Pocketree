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
        public string Description { get; set; }
        [Required]
        public string Difficulty { get; set; }
        public int CoinReward { get; set; }
        public bool RequiresEvidence { get; set; } // For ML Verification
        [Required, StringLength(80)]
        public string Keyword { get; set; } // For ML use
        [Required, StringLength(80)]
        public string Category { get; set; } // For ML use

        [NotMapped]
        public bool isCompleted { get; set; } // Not created in DB, field is just to match Android's side
    }
}
