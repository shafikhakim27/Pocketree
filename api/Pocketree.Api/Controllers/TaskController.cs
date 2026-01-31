using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using ADproject.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using System.ComponentModel.DataAnnotations.Schema;
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
        [HttpGet("GetDailyTasksApi")]
        public async Task<IActionResult> GetDailyTasksApi()
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Perform cleanup of tasks assigned on the day before
            await CleanupOldTasks(user);

            // Check if there are already existing tasks given
            var today = DateTime.UtcNow.Date;
            var currentDailyTasks = await db.UserTaskHistory
                .Where(h => h.UserID == user.UserID && h.CompletionDate >= today)
                .Include(h => h.Task)
                .ToListAsync();

            if (currentDailyTasks.Any())
            {
                // Return to Android the Task's status that matches the UserTaskHistory's status
                var taskStatus = currentDailyTasks.Select(h =>
                {
                    var t = h.Task;
                    t.isCompleted = (h.Status == "Completed");
                    return t;
                }).ToList();

                return Ok(taskStatus);
            }

            // First time receiving the tasks for the day
            List<Task> dailyTasks = await FetchNewTasks(user); 

            // Create a record in the UserTaskHistory table for the tasks given for the day 
            foreach(var t in dailyTasks)
            {
                db.UserTaskHistory.Add(new UserTaskHistory
                {
                    UserID = user.UserID,
                    TaskID = t.TaskID,
                    Status = "Assigned",
                    CompletionDate = DateTime.UtcNow
                });
            }

            // Update the number of uncompleted tasks assigned
            user.UncompletedTaskCount += 3;
            await db.SaveChangesAsync();

            return Ok(dailyTasks); // Sends JSON to Android
        }

        // Private helper function (do not make an API call to this)
        private async System.Threading.Tasks.Task CleanupOldTasks(User user)
        {
            var today = DateTime.UtcNow.Date;

            var previousDayTasks = await db.UserTaskHistory
                    .Where(h => h.UserID == user.UserID &&
                                h.Status == "Assigned" &&
                                h.CompletionDate < today)
                    .ToListAsync();

            if (previousDayTasks.Any())
            {
                user.NotAttemptedTaskCount += previousDayTasks.Count;   // Update the historical task counter
                user.UncompletedTaskCount -= previousDayTasks.Count;    // Update the daily active task counter

                db.UserTaskHistory.RemoveRange(previousDayTasks);
                await db.SaveChangesAsync();
            }
        }

        // Private helper function (do not make an API call to this)
        private async Task<List<Task>> FetchNewTasks(User user)
        {
            // Check user settings to determine ML recommended tasks or random tasks to be assigned
            var settings = await db.UserSettings
                .FirstOrDefaultAsync(s => s.UserID == user.UserID);

            List<Task> newTasks;

            // Use ML recommended tasks
            if (settings != null && settings.UseMlRecommendation)
            {
                newTasks = await mlService.GetRecommendedTasks(user.UserID);
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
                newTasks = new List<Task> { easyTask, normalTask, hardTask };
            }

            return newTasks;
        }

        [Authorize]
        [HttpPost("RecordTaskCompletionApi")]
        public async Task<IActionResult> RecordTaskCompletionApi([FromForm] TaskIdDto dto, IFormFile? photo)
        {
            var user = await db.Users
                .Include(u => u.Trees)
                .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            // Get the task details 
            var task = await db.Tasks.FindAsync(dto.TaskId);
            if (user == null || task == null) return BadRequest("Invalid User or Task.");

            // Perform verification check only for "Hard" tasks
            if (task.Difficulty == "Hard")
            {
                if (photo == null) return BadRequest("Photo evidence required for task.");
                using var stream = photo.OpenReadStream();
                if (!await mlService.ClassifyImageAsync(stream, task.Keyword)) // Reject submitted evidence after verification 
                {
                    user.FailedVerificationCount += 1;
                    await db.SaveChangesAsync();
                    return Ok(new { success = false });
                }
            }

            var result = await ProcessTaskCompletion(user, task); // Process task completion regardless of difficulty level
            return Ok(result);
        } 
        
        // Private helper function (do not make an API call to this)
        private async Task<object> ProcessTaskCompletion(User user, Task task)
        {
            // Start a transaction to update user task history, tree status, user total coins and new level if reached
            using var transaction = await db.Database.BeginTransactionAsync();
            try
            {
                // Search for the existing assigned record for today
                var today = DateTime.UtcNow.Date;
                var existingRecord = await db.UserTaskHistory
                    .FirstOrDefaultAsync(h => h.UserID == user.UserID &&
                                         h.TaskID == task.TaskID &&
                                         h.CompletionDate >= today &&
                                         h.Status == "Assigned");

                if (existingRecord != null)
                {
                    existingRecord.Status = "Completed";
                    existingRecord.CompletionDate = DateTime.UtcNow;
                }
                else
                {
                    return Ok(new { 
                        success = false,
                        LevelUp = false,
                        newCoins = user.TotalCoins,
                        newLevel = user.CurrentLevelID,
                        IsWithered = false
                    });
                }  
                
                // Update tree status
                var activeTree = user.Trees.FirstOrDefault(t => !t.IsCompleted);
                if (activeTree != null && activeTree.IsWithered)
                {
                    activeTree.IsWithered = false; // Tree is revived after the task completion
                }
                user.LastActivityDate = DateTime.UtcNow; // Update the activity date
                
                // Update user's uncompleted task count for the completed task
                user.UncompletedTaskCount -= 1;

                // Check and update level, coins and badges
                bool levelUp = await UpdateLevelAndCoins(user, task);
                await CheckAndAwardBadges(user);

                await db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send LevelUp, Coin balance, level and tree status to Android device              
                return new
                {
                    success = true,
                    LevelUp = levelUp,
                    NewCoins = user.TotalCoins,
                    NewLevel = user.CurrentLevelID,
                    IsWithered = activeTree?.IsWithered ?? false
                };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        [Authorize]
        [HttpPost("RedeemSkinApi")]
        public async Task<IActionResult> RedeemSkinApi([FromBody] int skinId)
        {
            var user = await db.Users
                 .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            var skin = await db.Skins.FindAsync(skinId);
            if (skin == null) return BadRequest("Requested skin cannot be found.");

            if (user.TotalCoins < skin.SkinPrice) return BadRequest("Insufficient coins.");

            // Redemption takes place
            user.TotalCoins -= skin.SkinPrice;

            // Update UserSkins 
            var userSkinEntry = new UserSkin
                {
                    UserID = user.UserID,
                    SkinID = skinId,
                    RedemptionDate = DateTime.UtcNow,
                    IsEquipped = true
                };

            db.UserSkins.Add(userSkinEntry);
            await db.SaveChangesAsync();

            return Ok(new { NewCoins = user.TotalCoins });
        }

        [Authorize]
        [HttpPost("RedeemVoucherApi")]
        public async Task<IActionResult> RedeemVoucherApi([FromBody] int VoucherId)
        {
            var user = await db.Users
                 .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            var voucher = await db.Vouchers.FindAsync(VoucherId);
            if (voucher == null) return BadRequest("Requested voucher cannot be found.");

            // Update UserVouchers 
            var userVoucherEntry = new UserVoucher
            {
                UserID = user.UserID,
                VoucherID = VoucherId,
                RedemptionCode = GenerateRedemptionCode(),    // To be filled up later
                RedemptionDate = DateTime.UtcNow,
                IsRedeemed = true
            };

            db.UserVouchers.Add(userVoucherEntry);
            await db.SaveChangesAsync();

            return Ok(new { IsRedeemed = true });
        }

        // Private function (not API) for backend use
        private string GenerateRedemptionCode()
        {
            // Define the pool of characters to ensure the code is readable and unique
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();

            // Return a 20-character random character string
            return new string(Enumerable.Repeat(chars, 20)
                .Select(s => s[random.Next(s.Length)]).ToArray());
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
