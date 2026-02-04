using ADproject.Hubs;
using ADproject.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pocketree.Api.Models.Entities;

namespace Pocketree.Api.Controllers
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly MyDbContext db;
        private readonly IHubContext<NotificationHub> hub;
        private readonly IPasswordHasher<User> passwordHasher;
        private readonly IConfiguration _configuration;

        public AdminApiController(MyDbContext db, IHubContext<NotificationHub> hub, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
        {    
            this.db = db;
            this.passwordHasher = passwordHasher;
            this.hub = hub;
            this._configuration = configuration;
        }

        // Obtain all the current Users that are online
        [HttpGet("FetchAllUsers")]
        public async Task<IActionResult> FetchAllUsers()
        {
            var allUsers = await db.Users
                .Where(u => u.UserRole == "Player")
                .Select(u => new { u.UserID, u.Username, u.Email })
                .ToListAsync();
            return Ok(allUsers);
        }

        // Obtain all the current Users that are online
        [HttpGet("FetchUsersOnline")]
        public async Task<IActionResult> FetchUsersOnline()
        {
            var usersOnline = await db.Users
                .Where(u => u.IsOnline == true && u.UserRole == "Player")
                .Select(u => new { u.UserID, u.Username, u.LastActivityDate })
                .ToListAsync();
            return Ok(usersOnline);
        }

        // Obtain all the queries sent by users
        [HttpGet("FetchUsersQueries")]
        public async Task<IActionResult> FetchUsersQueries()
        {
            var pendingQueries = await db.UserQueries
                .AsNoTracking()
                .Include(q => q.User)
                .Where(q => q.IsResolved == false) 
                .Select(q => new {
                    q.QueryID,
                    Username = q.User != null ? q.User.Username : "Unknown",
                    QueryContent = q.Query,
                    q.CreatedAt
                })
                .OrderByDescending(q => q.CreatedAt) // Latest to oldest query
                .ToListAsync();

            return Ok(pendingQueries);
        }

        // Clear user's pending query status when it is resolved
        [HttpPost("ClearUserQueryStatus")]
        public async Task<IActionResult> ClearUserQueryStatus([FromQuery] int queryId, [FromQuery] string reply)
        {
            var userQuery = await db.UserQueries.FindAsync(queryId);
            if (userQuery == null) return NotFound("User query not found.");

            userQuery.AdminReply = reply;
            userQuery.IsResolved = true;
            userQuery.ResolvedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await SendEmailAsync(userQuery.User.Email, "[PockeTree] RE: Query", $"Thank you for your query. {reply}\n\n Regards, Pocketree Admin");

            return Ok();
        }

        // Support password reset for users
        [HttpPost("ManualPasswordReset/{userId}")]
        public async Task<IActionResult> ManualPasswordReset(int userId)
        {
            var user = await db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Reset user's password
            user.PasswordHash = passwordHasher.HashPassword(user, "password");
            await db.SaveChangesAsync();
            return Ok("Password reset successfully.");
        }

        [HttpPost("SendPrivateMessage")]
        public async Task<IActionResult> SendPrivateMessage(int userId, string message)
        {
            // Get the AdminID
            var adminId = HttpContext.Session.GetString("AdminID");

            if (string.IsNullOrEmpty(adminId)) return Unauthorized("User details not found. Please login again.");

            // Ensure message is not empty before persisting to db
            if (string.IsNullOrWhiteSpace(message)) return BadRequest("Message cannot be empty");

            // Create a record in the Notificationmessage table
            var notification = new NotificationMessage
            {
                AdminID = int.Parse(adminId),
                Message = $"To UserId {userId}: {message}",
                TimeStamp = DateTime.UtcNow
            };

            db.NotificationMessages.Add(notification);
            await db.SaveChangesAsync();

            await hub.Clients.User(userId.ToString()).SendAsync("ReceiveMessage", message);
            
            return Ok();
        }

        // Helper function to send email reply to user
        private async System.Threading.Tasks.Task SendEmailAsync(string userEmail, string subject, string body)
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var appPassword = _configuration["EmailSettings:AppPassword"];

            using var client = new System.Net.Mail.SmtpClient(smtpServer)
            {
                Port = 587,
                Credentials = new System.Net.NetworkCredential(senderEmail, appPassword),
                EnableSsl = true,
            };

            var mailMessage = new System.Net.Mail.MailMessage(senderEmail, userEmail, subject, body);
            await client.SendMailAsync(mailMessage);
        }
    }
}
