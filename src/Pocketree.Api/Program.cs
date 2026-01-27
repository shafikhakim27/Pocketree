using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Required for UseMySql
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// --- 1. ML Service Configuration ---
// Uses Docker env var if available, otherwise falls back to the container name
string mlUrl = builder.Configuration["ML_SERVICE_URL"] ?? "http://ml-service:8000/";

builder.Services.AddHttpClient("ML_Consultant", client => {
    client.BaseAddress = new Uri(mlUrl);
});

// --- 2. Database Configuration ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? Environment.GetEnvironmentVariable("DEFAULT_CONNECTION");

builder.Services.AddDbContext<MyDbContext>(options => {
    // Safety check for connection string
    if (string.IsNullOrEmpty(connectionString)) 
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
        mySqlOptions => {
            // Fixes "Race Condition" where API starts before DB is ready
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
            );
        })
        .UseLazyLoadingProxies(); // ✅ Enable Lazy Loading here for Docker
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => 
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add other dependencies
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>(); 
// builder.Services.AddScoped<IMlService, MlService>();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, // Set to true if you have a specific issuer
        ValidateAudience = false, // Set to true if you have a specific audience
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("MySecretKeyMustBeAtLeast32CharsLong!!"))
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Pocketree API V1");
        // This ensures Swagger loads correctly at the root /swagger
        options.RoutePrefix = "swagger"; 
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession(); 

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize Database
initDB(); 

app.Run();

// --- Database Initialization Helper ---
// void initDB() 
// {
//     using (var scope = app.Services.CreateScope())
//     {
//         var ctx = scope.ServiceProvider.GetRequiredService<MyDbContext>();
//         try 
//         {
//             // Creates the DB if it doesn't exist
//             ctx.Database.EnsureCreated();
//         }
//         catch (Exception ex)
//         {
//             var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//             logger.LogError(ex, "An error occurred while initializing the database.");
//             throw; // Re-throw to ensure container restarts if DB fails
//         }
//     }
// }

void initDB() 
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        var ctx = services.GetRequiredService<MyDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        
        try 
        {
            ctx.Database.EnsureCreated();

            // 1. Seed Levels
            if (!ctx.Levels.Any())
            {
                ctx.Levels.AddRange(
                    new Level { LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
                    new Level { LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
                    new Level { LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
                );
                ctx.SaveChanges(); // Save to ensure Levels exist before Users reference them
            }

            // 2. Seed Tasks
            if (!ctx.Tasks.Any())
            {
                ctx.Tasks.AddRange(
                    new ADproject.Models.Entities.Task { Description = "Turn off lights when leaving a room", Difficulty = "Easy", Keyword = "switch", Category = "Energy Saving" },
                    new ADproject.Models.Entities.Task { Description = "Use a reusable water bottle all day", Difficulty = "Easy", Keyword = "bottle", Category = "Recycling" },
                    new ADproject.Models.Entities.Task { Description = "Compost your food scraps", Difficulty = "Normal", Keyword = "compost", Category = "Recycling" },
                    new ADproject.Models.Entities.Task { Description = "Take a 5-minute shower", Difficulty = "Normal", Keyword = "timer", Category = "Water Saving" },
                    new ADproject.Models.Entities.Task { Description = "Plant a physical tree or shrub", Difficulty = "Hard", Keyword = "tree", Category = "Nature" }
                );
            }

            // 3. Seed User (with proper password hashing)
            if (!ctx.Users.Any())
            {
                var testUser = new User
                {
                    Username = "EcoTester",
                    Email = "test@pocketree.com",
                    TotalCoins = 0,
                    CurrentLevelID = 1, // References 'Seedling'
                    ProfileImageURL = "https://www.pngarts.com/files/10/Default-Profile-Picture-PNG-Transparent-Image.png"
                };
                // Don't just save a string; hash it so your Login logic works!
                testUser.PasswordHash = hasher.HashPassword(testUser, "password123");
                
                ctx.Users.Add(testUser);
            }

            ctx.SaveChanges();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
            throw; 
        }
    }
}