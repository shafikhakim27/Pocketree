using ADproject.Models.Entities;
using Task = ADproject.Models.Entities.Task;

namespace ADproject.Services
{
    public interface IMlService
    {
        Task<bool> ClassifyImageAsync(Stream imageStream, string keyword);
        Task<List<Task>> GetRecommendedTasks(int userId);
    }
}
