namespace ADproject.Models.DTOs
{
    public class TaskCompletionResultDto
    {
        public bool success { get; set; }
        public string? Status { get; set; }
        public bool LevelUp { get; set; }
        public int NewCoins { get; set; }
        public int NewLevel { get; set; }
        public bool IsWithered { get; set; }
        public string NewLevelName { get; set; }
        public int PlantHealthPercent { get; set; }
    }
}