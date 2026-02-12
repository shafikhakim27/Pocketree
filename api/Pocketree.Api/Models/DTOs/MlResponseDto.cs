using ADproject.Models.DTOs;
using System.Text.Json.Serialization;

namespace Pocketree.Api.Models.DTOs
{
    public class MlResponseDto
    {
        // Match the JSON key "predictions"
        public List<PredictionDto> Predictions { get; set; }
    }

    public class PredictionDto
    {
        [JsonPropertyName("user_tier")]
        public string UserTier { get; set; }

        [JsonPropertyName("tasks")]
        public List<TaskDto> Tasks { get; set; }
    }

    public class TaskDto
    {
        public string Description { get; set; }
        public string Difficulty { get; set; }
        public int CoinReward { get; set; }
        public string Category { get; set; }
    }
}
