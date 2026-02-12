using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using Task = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Services
{
    public class TaskService : ITaskService
    {
        private readonly MyDbContext db;
        private readonly IMlService mlService;
        // Ensure Api only runs when it is not locked
        private static readonly SemaphoreSlim _semaphoreLock = new SemaphoreSlim(1, 1);

        public TaskService(MyDbContext db, IMlService mlService)
        {
            this.db = db;
            this.mlService = mlService;
        }

        // Perform house-keeping of tasks that are assigned by removing old previous day tasks
        public async System.Threading.Tasks.Task CleanupOldTasks(User user)
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

        // Api to get 3 new tasks (for default and/or ML-generated tass)
        public async System.Threading.Tasks.Task<List<Task>> FetchNewTasks(User user)
        {
            await _semaphoreLock.WaitAsync(); // Prevents other incoming requests to run the Api which may cause duplicate tasks
            try
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
                    .Where(t => t.Difficulty == "Easy" && t.SourceType == "Default")
                    .OrderBy(t => EF.Functions.Random())
                    .FirstOrDefaultAsync();

                    var normalTask = await db.Tasks
                        .Where(t => t.Difficulty == "Normal" && t.SourceType == "Default")
                        .OrderBy(t => EF.Functions.Random())
                        .FirstOrDefaultAsync();

                    var hardTask = await db.Tasks
                        .Where(t => t.Difficulty == "Hard" && t.SourceType == "Default")
                        .OrderBy(t => EF.Functions.Random())
                        .FirstOrDefaultAsync();

                    // Combine into a list of Tasks to send to Android
                    newTasks = new List<Task> { easyTask, normalTask, hardTask };
                }

                return newTasks;
            }
            finally
            {
                _semaphoreLock.Release(); // Release the lock so that can service other requests calling the Api
            }
        }
    }
}
