using ADproject.Hubs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pocketree.Api.Models.Entities;

namespace Pocketree.Api.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")] // Only allow Administrator to access the APIs
    public class AdminController : Controller
    {
        private readonly MyDbContext db;
        private readonly IHubContext<NotificationHub> hub;

        public AdminController(MyDbContext db, IHubContext<NotificationHub> hub, IPasswordHasher<User> passwordHasher)
        {
            this.db = db;
            this.hub = hub;
        }

        [HttpGet("/Admin/Index")]
        public async Task<IActionResult> Index()
        {
            // Check if the session exists
            var adminId = HttpContext.Session.GetString("AdminID");
            if (string.IsNullOrEmpty(adminId)) return RedirectToAction("Login", "User");

            var admin = await db.Users.FindAsync(int.Parse(adminId));
            if (admin == null) return NotFound();

            return View();
        }

        [HttpGet("/Admin/Logout")]
        public async Task<IActionResult> Logout()
        {
            var adminId = HttpContext.Session.GetString("AdminID");

            if (adminId != null)
            {
                var user = await db.Users.FindAsync(int.Parse(adminId));
                if (user != null)
                {
                    user.IsOnline = false;
                    await db.SaveChangesAsync();
                }
            }

            // Remove authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            // Remove all session data
            HttpContext.Session.Clear();

            return RedirectToAction("Login", "User");
        }

        [HttpPost("/Admin/BroadcastMessage")]
        public async Task<IActionResult> BroadcastMessage([FromForm] string messageText)
        {
            // Get the AdminID
            var adminId = HttpContext.Session.GetString("AdminID");

            if (string.IsNullOrEmpty(adminId)) return Unauthorized("User details not found. Please login again.");

            // Ensure message is not empty before persisting to db
            if (string.IsNullOrWhiteSpace(messageText)) return BadRequest("Message cannot be empty");

            // Create a record in the Notificationmessage table
            var notification = new NotificationMessage
            {
                AdminID = int.Parse(adminId),
                Message = $"To all users: {messageText}",
                TimeStamp = DateTime.UtcNow
            };

            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();

            // Send notification message to all devices connected on SignalR hub
            await hub.Clients.All.SendAsync("ReceiveMessage", messageText);

            return RedirectToAction("Index", "Admin");
        }
    }
}
