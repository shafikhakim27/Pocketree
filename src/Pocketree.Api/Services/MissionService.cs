using ADproject.Hubs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace ADproject.Services
{
    public class MissionService
    {
        private readonly MyDbContext db;
        private readonly IHubContext<MapHub> hub;

        public static readonly List<(double X, double Y)> locSlots = new List<(double X, double Y)>
        {
            // --- Phase 1: The Heart of the Sahara (Central) ---
            (48.0, 30.0), (52.0, 32.0), (45.0, 35.0), (55.0, 35.0), (50.0, 40.0),
            (42.0, 38.0), (58.0, 38.0), (47.0, 45.0), (53.0, 45.0), (50.0, 50.0),

            // --- Phase 2: Expanding North & West ---
            (35.0, 25.0), (40.0, 22.0), (45.0, 20.0), (55.0, 20.0), (60.0, 22.0),
            (30.0, 30.0), (32.0, 38.0), (28.0, 45.0), (35.0, 50.0), (25.0, 35.0),

            // --- Phase 3: Expanding East (The Horn Area) ---
            (65.0, 30.0), (70.0, 35.0), (75.0, 42.0), (78.0, 48.0), (72.0, 52.0),
            (68.0, 45.0), (62.0, 40.0), (75.0, 55.0), (80.0, 50.0), (70.0, 60.0),

            // --- Phase 4: Filling Southern Boundaries ---
            (40.0, 60.0), (45.0, 65.0), (50.0, 75.0), (55.0, 70.0), (60.0, 65.0),
            (48.0, 80.0), (52.0, 85.0), (58.0, 80.0), (42.0, 72.0), (38.0, 65.0),

            // --- Phase 5: Coastal & Edge Details ---
            (20.0, 40.0), (22.0, 50.0), (25.0, 60.0), (30.0, 70.0), (65.0, 20.0),
            (70.0, 25.0), (82.0, 55.0), (55.0, 90.0), (45.0, 88.0), (35.0, 80.0)
        };

        public MissionService (MyDbContext db, IHubContext<MapHub> hub)
        {
            this.db = db;
            this.hub = hub;
        }

        public async Task PlantNextTree(int missionId)
        {
            var mission = await db.GlobalMissions.FindAsync(missionId);
            if (mission == null) return;

            // Get the number of existing trees that have already been planted to find the index of the next tree to be planted
            int currentTreeCount = await db.CommunityForests.CountAsync();

            if (currentTreeCount < locSlots.Count)
            {
                var assignedSlot = locSlots[currentTreeCount];

                // Offset slightly with a small random number to position it naturally
                var random = new Random();
                double offsetX = (random.NextDouble() - 0.5) * 5; // +/- 2.5% offset
                double offsetY = (random.NextDouble() - 0.5) * 5;

                var newForestTree = new CommunityForest
                {
                    XCoordinate = assignedSlot.X + offsetX,
                    YCoordinate = assignedSlot.Y + offsetY,
                    MissionID = missionId,
                    PlantedAt = DateTime.UtcNow
                };

                // Plant global tree
                await hub.Clients.Group("WebDashboard")
                    .SendAsync("UpdateMapData", 
                    new { x = newForestTree.XCoordinate, y = newForestTree.YCoordinate});

                // Update db
                db.CommunityForests.Add(newForestTree);
                await db.SaveChangesAsync();
            }
        }
    }
}
