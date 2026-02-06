namespace ADproject.Models.DTOs
{
    public class RecommendedTasksDto
    {
        public string? Description { get; set; }
        public string? Difficulty { get; set; }
        public int CoinReward { get; set; }
        public string? Category { get; set; }
        public string? SourceType { get; set; }
    }
}