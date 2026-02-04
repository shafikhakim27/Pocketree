using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Models.ViewModels;
using Pocketree.Api.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Pocketree.Api.Models.DTOs;
using Pocketree.Api.Models.Entities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;


namespace ADproject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : Controller
    {
        private readonly IPasswordHasher<User> passwordHasher;
        private readonly MyDbContext db;
        private readonly IConfiguration _configuration;
        // Define withering threshold (3 days)
        private int witheringThreshold = 3;
        private readonly string baseURL;

        public UserController(MyDbContext db, IPasswordHasher<User> passwordHasher, IConfiguration configuration)
        {
            this.db = db;
            this.passwordHasher = passwordHasher;
            this._configuration = configuration;
            baseURL = _configuration["StorageBaseURL"] ?? "";
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
            newUser.ProfileImageURL = baseURL + "/images/default-user.jpg";
            newUser.TotalCoins = 0;
            newUser.CurrentLevelID = 1;
            newUser.LastLoginDate = DateTime.UtcNow;
            newUser.LastActivityDate = null;
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
            // fetch User object so we can update Login/Activity dates directly
            var user = await db.Users
                        .Include(u => u.CurrentLevel)
                        .Include(u => u.UserSkins)
                            .ThenInclude(us => us.Skin)
                        .Include(u => u.Trees) 
                        .FirstOrDefaultAsync(u => u.Username == dto.Username);

            if (user == null) return Unauthorized("Invalid credentials.");

            //var user = userData.User;
            var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

            if (result == PasswordVerificationResult.Success)
            {
                 if (user.UserRole == "Admin") 
                {
                    return BadRequest("Admins must use the Web portal.");
                }

                var activeTree = user.Trees?.FirstOrDefault(t => !t.IsCompleted);

                // Set tree status
                await CheckWithering(activeTree, user.LastActivityDate);

                user.IsOnline = true; // Set user's online status to true
                user.LastLoginDate = DateTime.UtcNow; // Update LastLoginDate
                
                await db.SaveChangesAsync();

                var androidProfile = GetAndroidUserProfile (user, activeTree, user.CurrentLevel?.LevelName);
                // var userProfile = GetUserProfile(user, activeTree, user.CurrentLevel?.LevelName);

                // GenerateJwtToken
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()), 
                    new Claim(ClaimTypes.Role, user.UserRole)
                };

                var token = GenerateJwtToken(claims);
                return Ok(new { Token = token, User = androidProfile });
            }

            return Unauthorized("Invalid credentials.");
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("LogoutApi")]
        public async Task<IActionResult> LogoutApi()
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == User.Identity.Name);
            if (user == null) return NotFound();
    
            user.IsOnline = false;
            await db.SaveChangesAsync();
            return Ok("Logged out successfully.");
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
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

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetUserProfileApi")]
        public async Task<IActionResult> GetUserProfileApi()
        {
            var userData = await db.Users
                    // .AsNoTracking() - remove so we can update tree status
                    .Where(u => u.Username == User.Identity.Name)
                    .Include(u => u.CurrentLevel)
                    .Include(u => u.UserSkins)
                        .ThenInclude(us => us.Skin)
                    .Include(u => u.Trees)
                    .Select(u => new
                    {
                        UserEntity = u, // so as to be able to access LastActivityDate to update tree status                        
                        u.Username,
                        u.TotalCoins,
                        u.CurrentLevelID,
                        u.ProfileImageURL,
                        u.LastActivityDate,
                        LevelName = u.CurrentLevel.LevelName,
                        // LevelImageURL = u.CurrentLevel.LevelImageURL,
                        ActiveTree = u.Trees.FirstOrDefault(t => !t.IsCompleted), // Get active tree
                    })
                    .FirstOrDefaultAsync();

            if (userData == null) return NotFound();

            await CheckWithering(userData.ActiveTree, userData.LastActivityDate);

            var androidProfile = GetAndroidUserProfile(userData.UserEntity, userData.ActiveTree, userData.LevelName);

            return Ok(androidProfile);
        }

            // // Equipping user's skin
            // bool isWithered = userData.ActiveTree?.IsWithered ?? false;

            // //dynamically concatenate image file names
            // string stageName = userData.LevelName.Split(' ')[0];
            // string skinSuffix = "";
            // string statusSuffix = "";

            // if (isWithered)
            // {
            //     finalPercent = 0;
            //     statusSuffix = "_Withered";     // Withered trees don't have skin
            // }
            // else
            // {
            //     if (userData.CurrentLevelID > 1) // cannot equip skin at Lv1
            //     {
            //         var equippedSkin = userData.User.UserSkins.FirstOrDefault(us => us.IsEquipped);
            //         if (equippedSkin != null)
            //         {
            //             skinSuffix = "_" + equippedSkin.Skin.SkinKey;
            //         }
            //     }
            // }

            // // get the whole file name, e.g. Tree_Sapling_Animals.png
            // string fileName = $"Tree_{stageName}{skinSuffix}{statusSuffix}.png";
            // string finalImageUrl = $"~/images/trees/{fileName}";
            
            // Prepare UserProfile data to send back to Android
            // var androidProfile = new AndroidUserProfileViewModel
            // {
            //     Username = userData.Username,
            //     TotalCoins = userData.TotalCoins,
            //     LevelName = userData.LevelName ?? "Seedling",
            //     LevelID = userData.CurrentLevelID,                    
            //     LevelImageURL = userData.LevelImageURL ?? "~/images/levels/seedling.png",
            //     ProfileImageURL = baseURL + (userData.ProfileImageURL ?? "~/images/default-user.jpg"),
            //     IsWithered = userData.ActiveTree?.IsWithered ?? false,              
            //     PlantHealthPercent = finalPercent
            // };

        // Private function (not API) for backend use
        private AndroidUserProfileViewModel GetAndroidUserProfile (User user, Tree? activeTree, string levelName)
        {
            double hoursSinceLastActivity = 0;
            if (user.LastActivityDate.HasValue)
            {
                hoursSinceLastActivity = (DateTime.UtcNow - user.LastActivityDate.Value).TotalHours;
            }
        
            var totalWindow = 72.0;
            var percent = (int)((1-(hoursSinceLastActivity/totalWindow)) * 100);
            int finalPercent = Math.Clamp(percent, 0, 100);

            // Check withering status
            bool isWithered = activeTree?.IsWithered ?? false;
                
            //dynamically concatenate image file names
            string stageName = (levelName ?? "Seedling").Split(' ')[0];
            // string stageName = userData.LevelName.Split(' ')[0];
            string skinSuffix = "";
            string statusSuffix = "";

            if (isWithered)
            {
                finalPercent = 0;
                statusSuffix = "_Withered";     // Withered trees don't have skin
            }
            else if (user.CurrentLevelID > 1) // cannot equip skin at Lv1
            {
                var equippedSkin = user.UserSkins?.FirstOrDefault(us => us.IsEquipped);
                if (equippedSkin?.Skin != null)
                {
                    skinSuffix = "_" + equippedSkin.Skin.SkinKey;
                }
            }

            // get the whole file name, e.g. Tree_Sapling_Animals.png
            string fileName = $"Tree_{stageName}{skinSuffix}{statusSuffix}.png";
            string finalImageUrl = baseURL + $"/images/trees/{fileName}";
        
            return new AndroidUserProfileViewModel
            {
                Username = user.Username,
                TotalCoins = user.TotalCoins,
                LevelName = levelName ?? "Seedling",
                LevelID = user.CurrentLevelID,
                LevelImageURL = finalImageUrl,
                ProfileImageURL = baseURL + (user.ProfileImageURL?.Replace("~/","")?? "images/default-user.jpg"),
                IsWithered = isWithered,
                // LevelImageURL = baseURL + (user.CurrentLevel?.LevelImageURL ?? "~/images/levels/seedling.png"),
                // ProfileImageURL = baseURL + (user.ProfileImageURL ?? "~/images/default-user.jpg"),
                // IsWithered = activeTree?.IsWithered ?? false,
                PlantHealthPercent = finalPercent
            };
        }

        // Private function (not API) for backend use
        private UserProfileViewModel GetUserProfile(User user, Tree? activeTree, string levelName)
        {
            return new UserProfileViewModel
            {
                Username = user.Username,
                TotalCoins = user.TotalCoins,
                LevelName = levelName ?? "Seedling",
                LevelID = user.CurrentLevelID,
                LevelImageURL = baseURL + (user.CurrentLevel?.LevelImageURL ?? "~/images/levels/seedling.png"),
                IsWithered = activeTree?.IsWithered ?? false
            };
        }

        // private function (not API) for backend use
        private async System.Threading.Tasks.Task CheckWithering(Tree? activeTree, DateTime? lastActivityDate)
        {
            if (activeTree == null || !lastActivityDate.HasValue) return;

            var daysNoActivity = (DateTime.UtcNow - lastActivityDate.Value).TotalDays;
            bool shouldBeWithered = daysNoActivity > witheringThreshold;

            // update if a change is required in Withered status
            if (activeTree.IsWithered != shouldBeWithered)
            {
                activeTree.IsWithered = shouldBeWithered;

                await db.SaveChangesAsync();
            }
        }

        // Private function (not API) for backend use
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

        // Provide all badges that the user has earned
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetLatestBadgesApi")]
        public async Task<IActionResult> GetLatestBadgesApi()
        {
            var username = User.Identity?.Name;
            var userId = await db.Users
                .AsNoTracking()
                .Where(u => u.Username == username)
                .Select(u => u.UserID)
                .FirstOrDefaultAsync();

            if (userId == 0) return Unauthorized();

            var latestBadges = await db.UserBadges
                .AsNoTracking()
                .Where(ub => ub.UserID == userId)
                .OrderByDescending(ub => ub.DateEarned) // From latest to oldest
                .Select(ub => new
                {
                    BadgeName = ub.Badge.BadgeName,
                    BadgeImageURL = baseURL + (ub.Badge.BadgeImageURL ?? "default-badge.png"),
                    DateEarned = ub.DateEarned
                })
                .ToListAsync();

            return Ok(latestBadges);
        }

        // Provide all skins that are offered to every user
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetAllSkinsOfferedApi")]
        public async Task<IActionResult> GetAllSkinsOfferedApi()
        {
            return Ok(await db.Skins
                    .AsNoTracking()
                    .Select(s => new
                    {
                        SkinName = s.SkinName,
                        SkinPrice = s.SkinPrice,
                        ImageURL = baseURL + (s.ImageURL ?? "default_skin.png")
                    })
                    .ToListAsync());
        }

        // Provide all skins that the user has redeemed
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetSkinsShopApi")]
        public async Task<IActionResult> GetSkinsShopApi()
        {
            var username = User.Identity?.Name;
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return Unauthorized();

            var allSkins = await db.Skins.AsNoTracking().ToListAsync();

            var userSkins = await db.UserSkins
                .Where(us => us.UserID == user.UserID)
                .ToListAsync();

            var shopList = allSkins.Select(skin =>
            {
                var ownedRecord = userSkins.FirstOrDefault(us => us.SkinID == skin.SkinID);

                return new SkinShopDto
                {
                    SkinID = skin.SkinID,
                    SkinName = skin.SkinName,
                    SkinPrice = skin.SkinPrice,
                    ImageURL = baseURL + (skin.ImageURL ?? "default_skin.png"),
                    IsRedeemed = (ownedRecord != null),
                    IsEquipped = (ownedRecord != null && ownedRecord.IsEquipped)
                };
            }).ToList();

            return Ok(shopList);
        }

        // Provide all vouchers that the user is awarded and can redeem
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        [HttpGet("GetAllVouchersApi")]
        public async Task<IActionResult> GetAllVouchersApi()
        {
            var username = User.Identity?.Name;
            var userId = await db.Users
                .AsNoTracking()
                .Where(u => u.Username == username)
                .Select(u => u.UserID)
                .FirstOrDefaultAsync();

            if (userId == 0) return Unauthorized();

            var allVouchers = await db.UserVouchers
                .AsNoTracking()
                .Where(uv => uv.UserID == userId)
                .Select(uv => new
                {
                    VoucherID = uv.Voucher.VoucherID,
                    VoucherName = uv.Voucher.VoucherName,
                    Description = uv.Voucher.Description,
                    RedemptionCode = uv.RedemptionCode,
                    IsRedeemed = uv.IsRedeemed
                })
                .ToListAsync();

            return Ok(allVouchers);
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
            newUser.ProfileImageURL = baseURL + "/images/default-user.jpg";
            newUser.TotalCoins = 0;
            newUser.CurrentLevelID = 1;
            newUser.LastLoginDate = DateTime.UtcNow;
            newUser.LastActivityDate = null;
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
                    user.IsOnline = true; // Set user's online status to true
                    user.LastLoginDate = DateTime.UtcNow; // Update LastLoginDate
                    db.Users.Update(user);
                    await db.SaveChangesAsync();

                    if (user.UserRole == "Player")
                    {
                        // Store player's info in Session
                        HttpContext.Session.SetString("UserID", user.UserID.ToString());
                        HttpContext.Session.SetString("Username", user.Username);
                        // Redirect to the status page of player
                        return RedirectToAction("Status", "User");
                    }
                    else // Admin who logs in
                    {
                        // Added for cookie authentication
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.Username),
                            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
                            new Claim(ClaimTypes.Role, user.UserRole)
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        // Sign in to the HttpContext to create the authentication cookie
                        var authProperties = new AuthenticationProperties
                        {
                            IsPersistent = true, // Keep logged in even if browser closes
                            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30) // Match session timeout period
                        };

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            authProperties);
                        
                        // Store administrator's info in Session
                        HttpContext.Session.SetString("AdminID", user.UserID.ToString());
                        HttpContext.Session.SetString("Username", user.Username);
                        // Redirect to the admin page
                        return RedirectToAction("Index", "Admin");
                    }
                }
            }

            ModelState.AddModelError("", "Login failed");
            return View(); // Remain on login page
        }

        [HttpGet("/User/Logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = HttpContext.Session.GetString("UserID");
            
            if (userId != null)
            {
                var user = await db.Users.FindAsync(int.Parse(userId));
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

        [HttpGet("/User/Status")]
        public async Task<IActionResult> Status()
        {
            // Check if the session exists
            var userId = HttpContext.Session.GetString("UserID");

            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "User");

            // Get User Data
            var user = await db.Users
                .Include(u => u.Trees)
                .Include(u => u.CurrentLevel)
                .FirstOrDefaultAsync(u => u.UserID == int.Parse(userId));

            if (user != null)
            {
                // Get active tree and update tree status
                var activeTree = user.Trees.FirstOrDefault(t => !t.IsCompleted);
 
                // Get the history of tasks performed by user
                var history = await db.UserTaskHistory
                    .Where(h => h.UserID == int.Parse(userId) && h.Status == "Completed")
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
                        LevelID = user.CurrentLevelID,
                        LevelImageURL = baseURL + (user.CurrentLevel?.LevelImageURL ?? "~/images/levels/seedling.png"),
                        ProfileImageURL = baseURL + (user.ProfileImageURL ?? "~/images/default-user.jpg"),
                        IsWithered = activeTree?.IsWithered ?? false
                    },
                    TaskHistory = history
                };

                await db.SaveChangesAsync();

                return View(compositeViewModel);
            }
            
            ModelState.AddModelError("", "User details cannot be retrieved!");
            return View();
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
                if (user.UserRole == "Player") return RedirectToAction("Status", "User");
                else return RedirectToAction("Index", "Admin");
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

        // Help users who forgotten their passwords to reset them
        [HttpPost("/User/PasswordReset")]
        public async Task<IActionResult> PasswordReset([FromForm] string username)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("Username not found.");

            // Generate random 8-char number code as the temporary password
            string tempPW = Guid.NewGuid().ToString().Substring(0, 8);
            user.PasswordHash = passwordHasher.HashPassword(user, tempPW);
            await db.SaveChangesAsync();

            // Send the temporary password to the user's registered email address
            await SendEmailAsync(user.Email, "[PockeTree] Reset Password - Auto-system generated reply", $"Your password has been reset to the following : {tempPW}. " +
                "Please remember to change your password after you have logged in.");

            return Ok("Password reset successfully and sent to your registered email");
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

        // Submit user query to administrator
        [HttpPost("/User/SubmitQuery")]
        public async Task<IActionResult> SubmitQuery([FromQuery] string identifier, [FromQuery] string userQuery)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);
            if (user == null) return BadRequest("User not found.");

            // Store the query in the UserQueries table
            db.UserQueries.Add(new UserQuery
            {
                UserID = user.UserID,
                Query = userQuery,
                IsResolved = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            return Ok("Query submitted successfully and our admin will get back to you.");
        }

        [AllowAnonymous]
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

        // Private function (not API) for backend use
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
                    IsCompleted = false
                };

                db.Trees.Add(initialTree);
                await db.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}
