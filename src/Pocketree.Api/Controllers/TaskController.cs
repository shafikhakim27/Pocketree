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
            var user = await db.Users
                .Include(u => u.Trees)
                .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Get the task details 
            var task = await db.Tasks.FindAsync(dto.TaskId);
            if (task == null) return BadRequest("Invalid Task");

            // Start a transaction to update user task history, tree status, user total coins and new level if reached
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

                // Update tree status
                var activeTree = user.Trees.FirstOrDefault(t => !t.IsCompleted);
                if (activeTree != null && activeTree.IsWithered)
                {
                    activeTree.IsWithered = false; // Tree is revived after the task completion
                }
                user.LastActivityDate = DateTime.UtcNow; // Update the activity date

                bool levelUp = await UpdateLevelAndCoins(user, task);

                await CheckAndAwardBadges(user);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send LevelUp, Coin balance, level and tree status to Android device              
                return Ok(new { 
                    LevelUp = levelUp,
                    NewCoins = user.TotalCoins,
                    NewLevel = user.CurrentLevelID,
                    IsWithered = activeTree?.IsWithered?? false
                });
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

        // Private function (not API) for backend use
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

        // Private function (not API) for backend use
        private async System.Threading.Tasks.Task CheckAndAwardBadges(User user)
        {
            // Get all badge IDs currently owned by user
            var currentBadgeIds = await db.UserBadges
                .Where(ub => ub.UserID == user.UserID)
                .Select(ub => ub.BadgeID)
                .ToListAsync();

            // Get all available badges
            var availableBadges = await db.Badges
                .Where(b => !currentBadgeIds.Contains(b.BadgeID))
                .ToListAsync();

            foreach (var badge in availableBadges)
            {
                bool eligibility = false;

                if (badge.CriteriaType == "LevelUp")
                {
                    eligibility = user.CurrentLevelID >= badge.RequiredCount;
                }
                else if (badge.CriteriaType == "TaskCount")
                {
                    int taskCount = await db.UserTaskHistory.CountAsync
                                            (th => th.UserID == user.UserID &&
                                             th.Task.Difficulty == badge.RequiredDifficulty);
                    eligibility = taskCount >= badge.RequiredCount;
                }

                // Award badges based on type if eligible
                if (eligibility == true)
                {
                    await AwardBadge(user.UserID, badge.BadgeID);
                }
            }
        }

        // Private function (not API) for backend use
        private async System.Threading.Tasks.Task AwardBadge(int userId, int badgeId)
        {
            var newBadge = new UserBadge
            {
                UserID = userId,
                BadgeID = badgeId,
                DateEarned = DateTime.UtcNow
            };

            db.UserBadges.Add(newBadge);
        }

        // Private function (not API) for backend use
        private async System.Threading.Tasks.Task ContributeToGlobalMission(User user, string missionName)
        {
            var mission = await db.GlobalMissions
                .Include(m => m.Trees)
                .FirstOrDefaultAsync(m => m.MissionName == missionName);

            if (mission != null)
            {
                mission.CurrentTreeCount++; // Increase global tree count
                
                // Get the tree for the specific mission
                var tree = mission.Trees.FirstOrDefault(t => t.UserID == user.UserID && !t.IsCompleted);
                if (tree != null) tree.IsCompleted = true;

                // Plant global tree if frequency met
                if (mission.CurrentTreeCount % mission.PlantingFrequency == 0)
                {
                    await missionService.PlantNextTree(mission.MissionID);
                }
            }
        }
    }
}
