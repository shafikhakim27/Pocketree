using System.ComponentModel.DataAnnotations;

namespace ADproject.Models.ViewModels
{
    public class TaskViewModel
    {
        public int TaskID { get; set; }
        public string Description { get; set; }
        public string Difficulty { get; set; }
        public int CoinReward { get; set; }
        public bool RequiresEvidence { get; set; } 
        public string Keyword { get; set; } 
    }
}
