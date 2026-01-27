using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using ADproject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using Task = ADproject.Models.Entities.Task;

namespace ADproject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaskController : ControllerBase
    {
        private readonly MyDbContext db;
        private readonly IMlService mlService;
        private readonly MissionService missionService;

        public TaskController(MyDbContext db, IMlService mlService, MissionService missionService)
        {
            this.db = db;
            this.mlService = mlService;
            this.missionService = missionService;
        }

        [Authorize]
        [HttpPost("GetDailyTasksApi")]
        public async Task<IActionResult> GetDailyTasksApi()
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Check user settings
            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserID == user.UserID);

            List<Task> dailyTasks;

            // Use ML recommended tasks
            if (settings != null && settings.UseMlRecommendation)
            {
                dailyTasks = await mlService.GetRecommendedTasks(user.UserID);
            }
            // Else fetch 1 task from each difficulty level randomly
            else
            {
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

                // Combine into a list of Tasks to send to Android
                dailyTasks = new List<Task> { easyTask, normalTask, hardTask };
            }
            
            return Ok(dailyTasks); // Sends JSON to Android
        }

        [Authorize]
        [HttpPost("RecordTaskCompletionApi")]
        public async Task<IActionResult> RecordTaskCompletionApi([FromBody] TaskIdDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Get the task details 
            var task = await db.Tasks.FindAsync(dto.TaskId);
            if (task == null) return BadRequest("Invalid Task");

            // Start a transaction to update user task history, user total coins and new level if reached
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                var taskHistoryEntry = new UserTaskHistory
                {
                    UserID = user.UserID,
                    TaskID = task.TaskID,
                    Status = "Completed",
                    CompletionDate = DateTime.UtcNow
                };
                db.UserTaskHistory.Add(taskHistoryEntry);

                bool levelUp = await UpdateLevelAndCoins(user, task);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send LevelUp status to Android device              
                return Ok(new { LevelUp = levelUp });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return BadRequest("Task not updated.");
            }
        }

        [Authorize]
        [HttpPost("SubmitTaskApi")]
        public async Task<IActionResult> SubmitTaskApi([FromForm] TaskIdDto dto, IFormFile photo)
        {
            var task = await db.Tasks.FindAsync(dto.TaskId);
            // Trigger ML to run verification
            using var stream = photo.OpenReadStream();
            bool isVerified = await mlService.ClassifyImageAsync(stream, task.Keyword);

            if (isVerified)
            {
                return Ok(new { success = "true" }); 
            }
            return BadRequest("Verification failed. Please try again.");
        }

        [Authorize]
        [HttpPost("UpdateCoinsApi")]
        public async Task<IActionResult> UpdateCoinsApi([FromBody] int CoinsBalance)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            user.TotalCoins = CoinsBalance;
            await db.SaveChangesAsync();
            return Ok(new { CoinsUpdated = "true" });
        }

        private async Task<bool> UpdateLevelAndCoins(User user, Task task)
        {
            user.TotalCoins += task.CoinReward;

            if (user.TotalCoins >= 500 && user.CurrentLevelID < 3)
            {
                user.CurrentLevelID = 3; // Set to new Mighty Oak level   
                await ContributeToGlobalMission(user, "Greenify Sahara"); // To specify MissionName for now 
                return true;
            }
            else if (user.TotalCoins >= 250 && user.CurrentLevelID < 2)
            {
                user.CurrentLevelID = 2; // Set to new Sapling level
                return true;
            }
            else return false;
        }

        private async System.Threading.Tasks.Task ContributeToGlobalMission(User user, string missionName)
        {
            var mission = await db.GlobalMissions
                .Include(m => m.Trees)
                .FirstOrDefaultAsync(m => m.MissionName == missionName);

            if (mission != null)
            {
                mission.CurrentTreeCount++; // Increase global tree count
                
                // Get the tree for the specific mission
                var tree = mission.Trees.FirstOrDefault(t => t.UserID == user.UserID && !t.ContributeToMission);
                if (tree != null) tree.ContributeToMission = true;

                // Plant global tree if frequency met
                if (mission.CurrentTreeCount % mission.PlantingFrequency == 0)
                {
                    await missionService.PlantNextTree(mission.MissionID);
                }
            }
        }
    }
}
