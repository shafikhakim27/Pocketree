using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using ADproject.Services;
using Pocketree.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Identity.Client;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.Eventing.Reader;
using System.Threading;
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
        private readonly ITaskService taskService;

        public TaskController(MyDbContext db, IMlService mlService, MissionService missionService, ITaskService taskService)
        {
            this.db = db;
            this.mlService = mlService;
            this.missionService = missionService;
            this.taskService = taskService;
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetDailyTasksApi")]
        public async Task<IActionResult> GetDailyTasksApi()
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Perform cleanup of tasks assigned on the day before
            await taskService.CleanupOldTasks(user);

            // Check if there are already existing tasks given
            var today = DateTime.UtcNow.Date; // = 00:00:00 (today midnight)
            var currentDailyTasks = await db.UserTaskHistory
                .Where(h => h.UserID == user.UserID && h.CompletionDate >= today)
                .Include(h => h.Task)
                .ToListAsync();

            if (currentDailyTasks.Any())
            {
                // Return to Android the Task's status that matches the UserTaskHistory's status
                var taskStatus = currentDailyTasks.DistinctBy(h => h.TaskID).Select(h =>
                {
                    var t = h.Task;
                    t.isCompleted = (h.Status == "Completed");
                    t.isPassed = (h.Status == "Passed");
                    return t;
                }).ToList();

                return Ok(taskStatus);
            }

            // First time receiving the tasks for the day
            List<Task> dailyTasks = await taskService.FetchNewTasks(user);

            if (dailyTasks != null || dailyTasks.Any())
            {
                // To save all new ML tasks to the Tasks table first so that TaskIDs needed for the UserTaskHistory records are generated
                foreach (var t in dailyTasks.Where(t => t.SourceType == "ML"))
                {
                    var exists = await db.Tasks.AnyAsync(task => task.Description == t.Description);
                    if (!exists)
                    {
                        db.Tasks.Add(t);
                    }
                }
                await db.SaveChangesAsync();

                // Create a record in the UserTaskHistory table for the tasks given for the day 
                foreach (var t in dailyTasks)
                {
                    var alreadyAddedTasks = await db.UserTaskHistory.AnyAsync(h => h.UserID == user.UserID
                        && h.CompletionDate >= today);

                    if (!alreadyAddedTasks)
                    {
                        db.UserTaskHistory.Add(new UserTaskHistory
                        {
                            UserID = user.UserID,
                            TaskID = t.TaskID,
                            Status = "Assigned",
                            CompletionDate = DateTime.UtcNow
                        });
                    }
                }

                // Update the number of uncompleted tasks assigned
                user.UncompletedTaskCount += 3;
                await db.SaveChangesAsync();

                return Ok(dailyTasks); // Sends JSON to Android
            }

            else
            {
                return StatusCode(503, "Timed out. Please try again.");
            }
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("RecordTaskCompletionApi")]
        public async Task<IActionResult> RecordTaskCompletionApi([FromForm] int taskId, [FromForm] string status, IFormFile? photo)
        {
            var user = await db.Users
                .Include(u => u.Trees)
                .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);

            // Get the task details 
            var task = await db.Tasks.FindAsync(taskId);
            if (user == null || task == null) return BadRequest("Invalid User or Task.");

            if (status == "Failed")
            {
                user.FailedVerificationCount += 1;
                await db.SaveChangesAsync();
                return Ok(new { success = false, message = "Recorded failed verification count." });
            }

            var result = await ProcessTaskCompletion(user, task, status); // Process task completion regardless of difficulty level
            return Ok(result);
        }
        
        // Private helper function (do not make an API call to this)
        private async Task<object> ProcessTaskCompletion(User user, Task task, string newStatus)
        {
            // Added to handle any transient or failed network connection
            var strategy = db.Database.CreateExecutionStrategy();

            // Get the program to retry the transaction below
            return await strategy.ExecuteAsync<object>(async () =>
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
                                            h.CompletionDate >= today 
                                            );

                    if (existingRecord == null) return new {success = false};
                    existingRecord.Status = newStatus; // can be "Completed" or "Passed"
                    existingRecord.CompletionDate = DateTime.UtcNow;

                    bool levelUp = false;
                    string newLevelName = "";
                    if (newStatus == "Completed")
                    {
                        // Update user's uncompleted task count for the completed task
                        user.UncompletedTaskCount -= 1;
                        // Check and update level, coins, badges and vouchers
                        var result = await UpdateLevelAndCoins(user, task);
                        levelUp = result.levelUp;
                        //new
                        if (levelUp) newLevelName = result.levelName;
                        await CheckAndAwardBadges(user);
                        await CheckAndAwardVouchers(user);
                    }

                    // Update tree status
                    var activeTree = user.Trees?.OrderByDescending(t => t.TreeID).FirstOrDefault();
                    if (activeTree != null && activeTree.IsWithered)
                    {
                        activeTree.IsWithered = false; // Tree is revived after the task completion
                    }
                    user.LastActivityDate = DateTime.UtcNow; // Update the activity date

                    await db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Send LevelUp, Coin balance, level and tree status to Android device              
                    return new TaskCompletionResultDto
                    {
                        success = true,
                        Status = newStatus,
                        LevelUp = levelUp,
                        NewCoins = user.TotalCoins,
                        NewLevel = user.CurrentLevelID,
                        IsWithered = activeTree?.IsWithered ?? false,
                        NewLevelName = newLevelName,
                        PlantHealthPercent = 100,
                    };
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        // Private function (not API) for backend use
        private async Task<(bool levelUp, string levelName)> UpdateLevelAndCoins(User user, Task task)
        {
            user.TotalCoins += task.CoinReward;

            if (user.TotalCoins >= 500 && user.CurrentLevelID < 3)
            {
                user.CurrentLevelID = 3; // Set to new Mighty Oak level   
                await ContributeToGlobalMission(user, "Greenify Sahara"); // To specify MissionName for now 
                return (true, "Mighty Oak");
            }
            else if (user.TotalCoins >= 250 && user.CurrentLevelID < 2)
            {
                user.CurrentLevelID = 2; // Set to new Sapling level
                return (true, "Sapling");
            }
            return (false,"");
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("RedeemSkinApi")]
        public async Task<IActionResult> RedeemSkinApi([FromBody] int skinId)
        {
            var user = await db.Users
                 .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            var existingSkin = await db.UserSkins
                .FirstOrDefaultAsync(us => us.UserID == user.UserID && us.SkinID == skinId);
            if (existingSkin != null) return BadRequest("You already owned this skin.");

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
                    IsRedeemed = true
                };

            db.UserSkins.Add(userSkinEntry);
            await db.SaveChangesAsync();

            return Ok(new { NewCoins = user.TotalCoins });
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("EquipSkinApi")]
        public async Task<IActionResult> EquipSkinApi([FromBody] int skinId)
        {
            var user = await db.Users
                 .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            // Retrieve the user skins from the UserSkins table
            var userSkinEntry = await db.UserSkins
                .FirstOrDefaultAsync(us => us.UserID == user.UserID && us.SkinID == skinId);

            if (userSkinEntry == null)
            {
                return BadRequest("You do not own this skin.");
            }
            
            // Set all skins currently equipped by this user to unequipped
            var currentlyEquippedSkins = await db.UserSkins
                .Where(us => us.UserID == user.UserID && us.IsEquipped)
                .ToListAsync();

            foreach (var s in currentlyEquippedSkins)
            {
                s.IsEquipped = false;
            }

            // Set the user's selected skin to equipped
            userSkinEntry.IsEquipped = true;

            await db.SaveChangesAsync();

            return Ok(new { success = true, message = "Skin equipped successfully!" });
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
        private async System.Threading.Tasks.Task CheckAndAwardVouchers(User user)
        {
            // Get all Voucher IDs that are already awarded to user
            var currentVoucherIds = await db.UserVouchers
                .Where(ub => ub.UserID == user.UserID)
                .Select(ub => ub.VoucherID)
                .ToListAsync();

            // Get all available vouchers
            var availableVouchers = await db.Vouchers
                .Where(b => !currentVoucherIds.Contains(b.VoucherID))
                .ToListAsync();

            foreach (var voucher in availableVouchers)
            {
                bool eligibility = false;
                eligibility = user.CurrentLevelID >= voucher.MinRedemptionLevel;

                // Award voucher if eligible
                if (eligibility == true)
                {
                    await AwardVoucher(user.UserID, voucher);
                }
            }
        }

        // Private function (not API) for backend use
        private async System.Threading.Tasks.Task AwardVoucher(int userId, Voucher voucher)
        {
            var newVoucher = new UserVoucher
            {
                UserID = userId,
                VoucherID = voucher.VoucherID,
                RedemptionCode = GenerateRedemptionCode()
            };

            db.UserVouchers.Add(newVoucher);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("RedeemVoucherApi")]
        public async Task<IActionResult> RedeemVoucherApi([FromBody] int VoucherId)
        {
            var user = await db.Users
                 .FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return Unauthorized();

            var voucher = await db.Vouchers.FindAsync(VoucherId);
            if (voucher == null) return BadRequest("Requested voucher cannot be found.");

            var userVoucherEntry = await db.UserVouchers.FirstOrDefaultAsync(uv => uv.UserID == user.UserID && uv.VoucherID == VoucherId);

            if (userVoucherEntry == null)
                return BadRequest("You do not possess this voucher.");

            if (userVoucherEntry.IsRedeemed)
                return BadRequest("Voucher already used.");

            userVoucherEntry.RedemptionDate = DateTime.UtcNow;
            userVoucherEntry.IsRedeemed = true;

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
