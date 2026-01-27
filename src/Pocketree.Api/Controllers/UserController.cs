using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Claims;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace ADproject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IPasswordHasher<User> passwordHasher;
        private readonly MyDbContext db;
        private readonly IConfiguration _configuration;

        public UserController(MyDbContext db, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
        {
            this.db = db;
            this.passwordHasher = passwordHasher;
            _configuration = configuration;
        }

        /**********************
          For all Api actions
        ***********************/

        [HttpPost("RegisterApi")]
        public async Task<IActionResult> RegisterApi([FromBody] UserRegistrationDto dto)
        {
            if (await db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest("Username is already taken.");

            var newUser = new User { Username = dto.Username };

            // Generate hash
            newUser.PasswordHash = passwordHasher.HashPassword(newUser, dto.Password);
            newUser.TotalCoins = 0;
            newUser.CurrentLevelID = 1;
            newUser.LastLoginDate = DateTime.UtcNow;
            newUser.Email = dto.Email;

            db.Users.Add(newUser);
            await db.SaveChangesAsync();

            // Proceed to plant a seed for the mission
            bool seedStatus = await PlantSeed(newUser.UserID);

            return Ok( new{ Success = seedStatus });
        }

        [HttpPost("LoginApi")]
        public async Task<IActionResult> LoginApi([FromBody] UserLoginDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Username == dto.Username);

            if (user == null) return Unauthorized("Invalid credentials.");

            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

            if (result == PasswordVerificationResult.Success)
            {
                user.LastLoginDate = DateTime.UtcNow; // Update LastLoginDate
                db.Users.Update(user);
                await db.SaveChangesAsync();

                // GenerateJwtToken
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                };

                var token = GenerateJwtToken(claims);

                return Ok(new { Token = token, user.UserID });
            }

            return Unauthorized("Invalid credentials.");
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePasswordApi([FromBody] ChangePasswordDto dto)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return NotFound();

            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);

            if (verificationResult == PasswordVerificationResult.Success)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
                db.Users.Update(user);
                await db.SaveChangesAsync();
                return Ok("Password updated successfully.");
            }
            return BadRequest("Current password is incorrect.");
        }

        [Authorize]
        [HttpGet("GetUserProfileApi")]
        public async Task<IActionResult> GetUserProfileApi()
        {
            // Define withering threshold (3 days)
            var witheringThreshold = DateTime.UtcNow.AddDays(-3);

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return NotFound();

            var userProfile = new UserProfileViewModel
            {
                Username = user.Username,
                TotalCoins = user.TotalCoins,
                LevelName = user.CurrentLevel?.LevelName ?? "Seedling",
                LevelID = user.CurrentLevelID,
                LevelImageURL = user.CurrentLevel?.LevelImageURL ?? "~/images/levels/seedling.png",
                // If the last login was earlier than the threshold, IsWithered = true 
                IsWithered = user.LastLoginDate < witheringThreshold
             };

            return Ok(userProfile);
        }

        private string GenerateJwtToken(Claim[] claims)
        {
            // Get secret key from appsettings.json
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Token details
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7), // Set 7 days for Android user to stay logged in
                signingCredentials: creds
            );

            // Serialize token as a string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<bool> PlantSeed(int userId)
        {
            var activeMission = await db.GlobalMissions
                                    .FirstOrDefaultAsync(m => m.MissionName == "Greenify Sahara");
            if (activeMission != null)
            {
                var initialTree = new Tree
                {
                    UserID = userId,
                    MissionID = activeMission.MissionID,
                    ContributeToMission = false
                };

                db.Trees.Add(initialTree);
                await db.SaveChangesAsync();
                return true;
            }
            return false;
        }


        /**********************
          For all Web actions
        ***********************/

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
            newUser.LastLoginDate = DateTime.UtcNow;
            newUser.Email = dto.Email;

            db.Users.Add(newUser);
            await db.SaveChangesAsync();

            // Proceed to plant a seed for the mission
            bool seedStatus = await PlantSeed(newUser.UserID);

            // Redirect to login page after successful sign-up
            if (seedStatus == true) 
                TempData["Message"] = "Successfully registered.";
            else
                ModelState.AddModelError("", "Registration failed");
            
            return RedirectToAction("Login", "User");
        }

        // Show the Login Page
        [HttpGet("/User/Login")]
        public IActionResult Login()
        {
            return View();
        }

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
                    user.LastLoginDate = DateTime.UtcNow; // Update LastLoginDate
                    db.Users.Update(user);
                    await db.SaveChangesAsync();

                    // Store user info in Session
                    HttpContext.Session.SetString("UserID", user.UserID.ToString());
                    HttpContext.Session.SetString("Username", user.Username);

                    // Redirect to the status page
                    return RedirectToAction("Status", "User");
                }
            }

            ModelState.AddModelError("", "Login failed");
            return View(); // Remain on login page
        }

        [Route("/User/Logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpGet("/User/Status")]
        public async Task<IActionResult> Status()
        {
            // Check if the session exists
            var userId = HttpContext.Session.GetString("UserID");

            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "User");

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

            // Define withering threshold (3 days)
            var witheringThreshold = DateTime.UtcNow.AddDays(-3);

            // Combine both data for the Status Page
            var compositeViewModel = new StatusPageViewModel
            {
                UserProfile = new UserProfileViewModel
                {
                    Username = user.Username,
                    TotalCoins = user.TotalCoins,
                    LevelName = user.CurrentLevel?.LevelName ?? "Seedling",
                    LevelID = user.CurrentLevelID,
                    LevelImageURL = user.CurrentLevel?.LevelImageURL ?? "~/images/levels/seedling.png",
                    // If the last login was earlier than the threshold, IsWithered = true 
                    IsWithered = user.LastLoginDate < witheringThreshold
                },
                TaskHistory = history
            };

            return View(compositeViewModel);
        }

        // Show the Change Password Page
        [HttpGet("/User/ChangePassword")]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost("/User/ChangePassword")]
        public async Task<IActionResult> ChangePassword([FromForm] ChangePasswordDto dto)
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Login", "User");

            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();

            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);

            if (verificationResult == PasswordVerificationResult.Success)
            {
                user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
                db.Users.Update(user);
                await db.SaveChangesAsync();

                TempData["Message"] = "Password updated successfully!";
                return RedirectToAction("Status");
            }

            ModelState.AddModelError("", "Current password is incorrect.");
            return View();
        }

        [HttpPost("/User/UploadProfilePicture")]
        public async Task<IActionResult> UploadProfilePicture(IFormFile profileFile)
        {
            var userId = HttpContext.Session.GetString("UserID");
            if (profileFile == null || string.IsNullOrEmpty(userId))
                return RedirectToAction("Profile");

            // Create a unique filename
            string filename = Guid.NewGuid().ToString() + ".jpg";
            string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            // Ensure the directory exists
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            string fullPath = Path.Combine(uploadDir, filename);

            // Load, Resize and Compress
            using (var image = await Image.LoadAsync(profileFile.OpenReadStream()))
            {
                // Resize to 400 x 400 and maintain aspect ratio
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(400, 400),
                    Mode = ResizeMode.Max
                }));

                // Save as JPEG with 75% quality to save space
                await image.SaveAsJpegAsync(fullPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
                {
                    Quality = 75
                }); 
            }

            // Update User record in database
            var user = await db.Users.FindAsync(int.Parse(userId));
            user.ProfileImageURL = "/uploads/" + filename;
            await db.SaveChangesAsync();

            return RedirectToAction("Profile");
        }

        [HttpGet("/User/Profile")]
        public async Task<IActionResult> Profile()
        {
            var sessionValue = HttpContext.Session.GetString("UserID");
            if (!int.TryParse(sessionValue, out int userId))
                return RedirectToAction("Login");

            var user = await db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpGet("/User/Settings")]
        public async Task<IActionResult> Settings()
        {
            var sessionValue = HttpContext.Session.GetString("UserID");
            if (!int.TryParse(sessionValue, out int userId))
                return RedirectToAction("Login");

            var userSettings = await db.UserSettings.FindAsync(userId);
            if (userSettings == null)
            {
                userSettings = new UserSettings { UserID = userId };
                db.UserSettings.Add(userSettings);
                await db.SaveChangesAsync();
            }
            
            return View(userSettings);
        }

        [HttpPost("/User/UpdateSettings")]
        public async Task<IActionResult> UpdateSettings([FromForm] UserSettings settings)
        {
            if (ModelState.IsValid)
            {
                db.UserSettings.Update(settings);
                await db.SaveChangesAsync();
                TempData["Success"] = "Settings updated successfully!";
                return RedirectToAction("Status", "User");
            }
            return View("Settings", settings);
        }

        [HttpPost("/User/WaterTree")]
        public async Task<IActionResult> WaterTree()
        {
            // Check if the session exists
            var userId = HttpContext.Session.GetString("UserID");

            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "User");

            // Get User Data
            var user = await db.Users.FindAsync(int.Parse(userId));

            if (user != null)
            {
                user.LastLoginDate = DateTime.UtcNow; // Update latest LastLoginDate
                db.Users.Update(user);
                await db.SaveChangesAsync();
            }

            TempData["Message"] = "Thank you for taking care of me! I am so happy now!";
            return RedirectToAction("Status", "User");
        }
    }
}
