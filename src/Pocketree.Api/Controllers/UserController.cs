using ADproject.Models.Entities;
using ADproject.Models.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ADproject.Models.ViewModels;

namespace ADproject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IPasswordHasher<User> passwordHasher;
        private readonly MyDbContext db;

        public UserController(MyDbContext db, IPasswordHasher<User> passwordHasher)
        {
            this.db = db;
            this.passwordHasher = passwordHasher;
        }

        // For Api
        [HttpPost("RegisterApi")]
        public async Task<IActionResult> RegisterApi([FromBody] UserRegistrationDto dto)
        {
            if (db.Users.Any(u => u.Username == dto.Username))
                return BadRequest("Username is already taken.");

            var newUser = new User { Username = dto.Username };

            // Generate hash
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, dto.Password);
            newUser.TotalCoins = 0;
            newUser.CurrentLevelID = 1;
            newUser.LastLoginDate = DateTime.Now;

            db.Users.Add(newUser);
            await db.SaveChangesAsync();
            return Ok("Registration successful.");
        }

        // For API
        [HttpPost("LoginApi")]
        public async Task<IActionResult> LoginApi([FromBody] UserLoginDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Username == dto.Username);

            if (user == null) return Unauthorized("Invalid credentials.");

            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

            if (result == PasswordVerificationResult.Success)
            {
                return Ok(new { user.UserID, user.Username });
            }
            return Unauthorized("Invalid credentials.");
        }

        // For Web
        // Show the Registration Page
        [HttpGet("/User/Register")]
        public IActionResult Register()
        {
            return View();
        }

        // Process the Registration
        [HttpPost("/User/Register")]
        public async Task<IActionResult> Register([FromForm] UserRegistrationDto dto)
        {
            if (!ModelState.IsValid) return View(dto);

            // Check if user already exists
            if (await db.Users.AnyAsync(u => u.Username == dto.Username))
            {
                ModelState.AddModelError("Username", "Username is already taken.");
                return View(dto);
            }

            // Create the new user with hashed password
            var newUser = new User { Username = dto.Username };

            // Generate hash
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, dto.Password);
            newUser.TotalCoins = 0;
            newUser.CurrentLevelID = 1;
            newUser.LastLoginDate = DateTime.Now;

            db.Users.Add(newUser);
            await db.SaveChangesAsync();

            // Redirect to login page after successful sign-up
            return RedirectToAction("Index", "Home");
        }

        // For Web
        [HttpPost("/User/Login")]
        public async Task<IActionResult> Login([FromForm] UserLoginDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Username == dto.Username);

            if (user != null)
            {
                var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
                if (result == PasswordVerificationResult.Success)
                {
                    // Store user info in Session
                    HttpContext.Session.SetString("UserID", user.UserID.ToString());
                    HttpContext.Session.SetString("Username", user.Username);

                    // Redirect to the status page
                    return RedirectToAction("Status", "User");
                }
            }

            ModelState.AddModelError("", "Login failed");
            return RedirectToAction("Index", "Home"); // Return to landing page
        }

        public async Task<IActionResult> Status()
        {
            // Check if the session exists
            var userId = HttpContext.Session.GetString("UserID");

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Index", "Home");
            }

            // Get User Data
            var user = await db.Users
                .Include(u => u.CurrentLevel)
                .FirstOrDefaultAsync(u => u.UserID == int.Parse(userId));

            // Get the history of tasks performed by user
            var history = await db.UserTaskHistory
                .Where(h => h.UserID == int.Parse(userId))
                .Include(h => h.Task)
                .OrderByDescending(h => h.CompletionDate)
                .Select(h => new TaskHistoryViewModel
                {
                    TaskDescription = h.Task.Description,
                    DateCompleted = h.CompletionDate.ToString("dd MMM yyyy"),
                    CoinsEarned = h.Task.CoinReward
                }).ToListAsync();

            // Combine both data for the Status Page
            var compositeViewModel = new StatusPageViewModel
            {
                UserProfile = new UserProfileViewModel
                {
                    Username = user.Username,
                    TotalCoins = user.TotalCoins,
                    LevelName = user.CurrentLevel?.LevelName ?? "Seedling",
                    LevelID = user.CurrentLevelID
                },
                TaskHistory = history 
            };

            return View(compositeViewModel);
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return NotFound();

            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);

            if (verificationResult == PasswordVerificationResult.Success)
            {
                db.Users.Update(user);
                await db.SaveChangesAsync();
                return Ok("Password updated successfully.");
            }
            return BadRequest("Current password is incorrect.");
        }
    }
}
