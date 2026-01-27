using ADproject.Hubs;
using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Task = System.Threading.Tasks.Task;

namespace ADproject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContributionController : ControllerBase
    {
        private readonly MyDbContext db;
        private readonly IHubContext<MapHub> hub;

        public ContributionController(MyDbContext db, IHubContext<MapHub> hub)
        {
            this.db = db;
            this.hub = hub;
        }

        [AllowAnonymous]
        [HttpGet("GetAllForestTrees")]
        public async Task<ActionResult<IEnumerable<TreeCoordinateDto>>> GetAllForestTrees()
        {
            // Fetch all coordinates from the CommunityForest table
            var trees = await db.CommunityForests
                .Select(t => new TreeCoordinateDto
                {
                    XCoordinate = t.XCoordinate,
                    YCoordinate = t.YCoordinate
                })
                .ToListAsync();

            return Ok(trees);
        }
    }
}
