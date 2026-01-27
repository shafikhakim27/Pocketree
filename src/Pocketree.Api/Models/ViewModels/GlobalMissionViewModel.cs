namespace ADproject.Models.ViewModels
{
    public class GlobalMissionViewModel
    {
        public string MissionName { get; set; }
        public int TargetTrees { get; set; }
        public int CurrentTrees { get; set; }
        public double ProgressPercentage => (double)CurrentTrees / TargetTrees * 100;
    }
}
