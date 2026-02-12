using ADproject.Models.Entities;
using Google.Api;
using Microsoft.EntityFrameworkCore;
using Task = ADproject.Models.Entities.Task;

namespace Pocketree.Api.Services
{
    public class DailyTaskGenerator : BackgroundService
    {
        private readonly IServiceProvider services;

        public DailyTaskGenerator(IServiceProvider services)
        {
            this.services = services;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial startup wait
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine(">>> Background Task Generator: Starting...");

                List<User> users;

                using (var scope = services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                    users = await db.Users.ToListAsync();
                }

                foreach (var user in users)
                {
                    using (var userScope = services.CreateScope())
                    {
                        var db = userScope.ServiceProvider.GetRequiredService<MyDbContext>();
                        var taskService = userScope.ServiceProvider.GetRequiredService<ITaskService>();
                        var today = DateTime.UtcNow.Date;

                        try
                        {
                            await taskService.CleanupOldTasks(user);

                            var alreadyAssigned = await db.UserTaskHistory
                                    .AnyAsync(h => h.UserID == user.UserID && h.CompletionDate >= today);

                            if (!alreadyAssigned)
                            {
                                List<Task> dailyTasks = await taskService.FetchNewTasks(user);
                                if (dailyTasks != null && dailyTasks.Any())
                                {
                                    // Save ML Parent Tasks
                                    foreach (var t in dailyTasks.Where(t => t.SourceType == "ML"))
                                    {
                                        var exists = await db.Tasks.AnyAsync(task => task.Description == t.Description);
                                        if (!exists) db.Tasks.Add(t);
                                    }
                                    await db.SaveChangesAsync();

                                    // Save User History
                                    foreach (var t in dailyTasks)
                                    {
                                        db.UserTaskHistory.Add(new UserTaskHistory
                                        {
                                            UserID = user.UserID,
                                            TaskID = t.TaskID,
                                            Status = "Assigned",
                                            CompletionDate = DateTime.UtcNow
                                        });
                                    }

                                    // Update count only once per user
                                    var userToUpdate = await db.Users.FindAsync(user.UserID);
                                    userToUpdate.UncompletedTaskCount += 3;
                                    await db.SaveChangesAsync();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error for {user.Username}: {ex.Message}");
                        }
                    }
                }
                
                Console.WriteLine(">>> Background Task Generator: Completed...");
                
                // For initial logic testing and also to check that the service triggers (at 3 min interval)
                // await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(180), stoppingToken);

                // Calculate delay to next midnight Sg time
                var currentSgTime = DateTime.UtcNow.AddHours(8);
                var nextRunSgTime = currentSgTime.Date.AddHours(6);

                // Handles the "Restart after 6am" safety check
                if (currentSgTime >= nextRunSgTime)
                {
                    nextRunSgTime = nextRunSgTime.AddDays(1);
                }

                var delay = nextRunSgTime - currentSgTime;
                await System.Threading.Tasks.Task.Delay(delay, stoppingToken);
            }
        }
    }
}
