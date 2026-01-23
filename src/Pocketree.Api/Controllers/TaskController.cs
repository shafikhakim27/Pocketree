using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using ADproject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Task = ADproject.Models.Entities.Task;

namespace ADproject.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly MyDbContext db;
        private readonly IMlService mlService;
        public TaskController(MyDbContext db, IMlService mlService)
        {
            this.db = db;
            this.mlService = mlService;
        }

        public async Task<IActionResult> GetDailyTasksApi()
        {
            // Fetch 1 task from each difficulty level randomly
            var easyTask = await db.Tasks
                .Where(t => t.Difficulty == "Easy")
                .OrderBy(t => EF.Functions.Random()) 
                .FirstOrDefaultAsync(); 

            var normalTask = await db.Tasks
                .Where(t => t.Difficulty == "Normal")
                .OrderBy(t => EF.Functions.Random())
                .FirstOrDefaultAsync();

            var hardTask = await db.Tasks
                .Where(t => t.Difficulty == "Hard")
                .OrderBy(t => EF.Functions.Random())
                .FirstOrDefaultAsync();

            // Combine into a list of TaskViewModel to send to Android
            List<TaskViewModel> dailyTasks = new List<Task> { easyTask, normalTask, hardTask }
                .Where(t => t != null)
                .Select(t => new TaskViewModel
                {
                    TaskID = t.TaskID,
                    Description = t.Description,
                    Difficulty = t.Difficulty,
                    CoinReward = t.CoinReward,
                    RequiresEvidence = t.RequiresEvidence,  
                    Keyword = t.Keyword  
                })
                .ToList();

                return Ok(dailyTasks); // Sends JSON to Android
        }

        public async Task<bool> RecordTaskCompletionApi([FromForm] int userId, [FromForm] int taskId)
        {
            // Get the task details 
            var task = await db.Tasks.FindAsync(taskId);
            if (task == null) return false;

            // Start a transaction to update both user task history and user total coins
            using var transaction = await 
                db.Database.BeginTransactionAsync();
            try
            {
                var taskHistoryEntry = new UserTaskHistory
                {
                    UserID = userId,
                    TaskID = taskId,
                    Status = "Completed",
                    CompletionDate = DateTime.Now
                };
                db.UserTaskHistory.Add(taskHistoryEntry);

                var user = await db.Users.FindAsync(userId);
                user.TotalCoins += task.CoinReward;

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return false;
            }
        }

        public async Task<IActionResult> SubmitTaskApi([FromForm] int userId, [FromForm] int taskId, IFormFile photo)
        {
            var task = await db.Tasks.FindAsync(taskId);
            // Trigger ML to run verification
            using var stream = photo.OpenReadStream();
            bool isVerified = await mlService.ClassifyImageAsync(stream, task.Keyword);

            if (isVerified)
            {
                // Record as completed and award 30 coins
                await RecordTaskCompletionApi(userId, taskId);
                return Ok(new { success = "true" }); 
            }
            return BadRequest("Verification failed. Please try again.");
        }
    }
}
