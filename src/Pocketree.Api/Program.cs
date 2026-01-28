using ADproject.Hubs; 
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;
using Task = ADproject.Models.Entities.Task;

var builder = WebApplication.CreateBuilder(args);

// For turning on Scalar and testing
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new Microsoft.OpenApi.Models.OpenApiComponents();

        // Define the Security Scheme
        var securityScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token here."
        };

        document.Components.SecuritySchemes.Add("Bearer", securityScheme);

        // Apply it globally to all endpoints
        document.SecurityRequirements.Add(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            [new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            }] = Array.Empty<string>()
        });

        return System.Threading.Tasks.Task.CompletedTask;
    });
});

// Register the HttpClient for Python communication
builder.Services.AddHttpClient("ML_Consultant", client => {
    client.BaseAddress = new Uri("http://localhost:5000/");
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add database context dependency
builder.Services.AddDbContext<MyDbContext>();
// Add other dependencies needed
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>();
builder.Services.AddScoped<IMlService, MlService>();
builder.Services.AddScoped<MissionService>();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session expires after 30 mins
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add SignalR services
builder.Services.AddSignalR();

// Add the context accessor to use Session in Views
builder.Services.AddHttpContextAccessor();

// Add Authentication Services
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
        NameClaimType = ClaimTypes.Name
    };
});

var app = builder.Build();

// For turning on Scalar
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Serves the JSON
    app.MapScalarApiReference(); // Serves the UI at /scalar/v1
}

app.MapHub<MapHub>("/mapHub");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseSession(); // Enable Session middleware

app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await initDB(app.Services); 
app.Run();

// init database
async System.Threading.Tasks.Task initDB(IServiceProvider services)
{
    // create the environment to retrieve our database context
    using var scope = services.CreateScope();
    {
        // get database context from DI-container
        var ctx = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        await ctx.Database.EnsureCreatedAsync();

        if (!ctx.Levels.Any())
        {
            // Add Levels
            ctx.Levels.AddRange(
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "/images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "/images/levels/sapling.png" },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "/images/levels/oak.png" }
            );

            // Add Tasks
            ctx.Tasks.AddRange(
                new Task { TaskID = 1, Description = "Turn off lights when leaving a room", Difficulty = "Easy", CoinReward = 100, RequiresEvidence = false, Keyword = "switch", Category = "Energy Saving" },
                new Task { TaskID = 2, Description = "Use a reusable water bottle all day", Difficulty = "Easy", CoinReward = 100, RequiresEvidence = false, Keyword = "bottle", Category = "Recycling" },
                new Task { TaskID = 3, Description = "Compost your food scraps", Difficulty = "Normal", CoinReward = 200, RequiresEvidence = false, Keyword = "compost", Category = "Recycling" },
                new Task { TaskID = 4, Description = "Take a 5-minute shower", Difficulty = "Normal", CoinReward = 200, RequiresEvidence = false, Keyword = "timer", Category = "Water Saving" },
                new Task { TaskID = 5, Description = "Plant a physical tree or shrub", Difficulty = "Hard", CoinReward = 300, RequiresEvidence = false, Keyword = "tree", Category = "Nature" }
            );

            // Add Badges
            ctx.Badges.AddRange(
                new Badge { BadgeID = 1, BadgeName = "Tree Starter", Description = "This badge is awarded to player who reaches Level 2", BadgeImageURL = "", CriteriaType = "LevelUp", RequiredDifficulty = "Any", RequiredCount = 2 },
                new Badge { BadgeID = 2, BadgeName = "Mighty Oak", Description = "This badge is awarded to player who reaches Level 3", BadgeImageURL = "", CriteriaType = "LevelUp", RequiredDifficulty = "Any", RequiredCount = 3 },
                new Badge { BadgeID = 3, BadgeName = "Green Starter", Description = "This badge is awarded to player who completed 30 Easy tasks", BadgeImageURL = "", CriteriaType = "TaskCount", RequiredDifficulty = "Easy", RequiredCount = 30 },
                new Badge { BadgeID = 4, BadgeName = "Green Champion", Description = "This badge is awarded to player who completed 20 Normal tasks", BadgeImageURL = "", CriteriaType = "TaskCount", RequiredDifficulty = "Normal", RequiredCount = 20 },
                new Badge { BadgeID = 5, BadgeName = "Eco Warrior", Description = "This badge is awarded to player who completed 10 Hard tasks", BadgeImageURL = "", CriteriaType = "TaskCount", RequiredDifficulty = "Hard", RequiredCount = 10 }
            );

            // Add initial Test User 
            ctx.Users.Add(new User
            {
                UserID = 1,
                Username = "ecotester",
                PasswordHash = "AQAAAAIAAYagAAAAEMO7BqP3P6mwKCn+y4U448SilNgQsmcaKZlFou2pu3x/3EiFixI8pLMryKFzJWQbOA==", 
                ProfileImageURL = "",
                TotalCoins = 0,
                CurrentLevelID = 1,
                LastLoginDate = DateTime.UtcNow,
                LastActivityDate = null,
                Email = "ecotester@gmail.com"
            });

            // Add UserPreferences
            ctx.UserPreferences.AddRange(
                new UserPreference { PreferenceID = 1, UserID = 1, PreferredCategory = "Recycling", PreferredDifficulty = "Easy"}
            );

            await ctx.SaveChangesAsync();
        }
    }
}




