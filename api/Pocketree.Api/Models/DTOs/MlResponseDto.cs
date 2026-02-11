using ADproject.Models.DTOs;
using System.Text.Json.Serialization;

namespace Pocketree.Api.Models.DTOs
{
    public class MlResponseDto
    {
        [JsonPropertyName("task_id")]
        public string TaskID { get; set; }
        [JsonPropertyName("user_tier")]
        public string UserTier { get; set; }
        [JsonPropertyName("tasks")]
        public List<RecommendedTasksDto> Tasks { get; set; }
    }
}
