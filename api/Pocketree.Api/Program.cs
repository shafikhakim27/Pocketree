using ADproject.Hubs; 
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pocketree.Api.Hubs;
using Pocketree.Api.Services;
using Scalar.AspNetCore;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
var mlServiceUrl = builder.Configuration["MlService:Url"] ?? "http://localhost:5000/";
var mlRecommendUrl = builder.Configuration["MlService:RecommendUrl"] ?? mlServiceUrl;
builder.Services.AddHttpClient("ML_Consultant", client => {
    client.BaseAddress = new Uri(mlRecommendUrl);
});

// Add CORS policy for API access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder =>
        {
            builder .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
        });
});

// Add services to the container.
builder.Services.AddControllersWithViews();
// 1. RETRIEVE the connection string from appsettings or Docker env vars
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Add database context dependency
// Avoid ServerVersion.AutoDetect during tests because it connects to the DB immediately.
var serverVersion = builder.Environment.IsEnvironment("Testing")
    ? ServerVersion.Parse("8.0.21") // Use a fixed version for tests
    : ServerVersion.AutoDetect(connectionString);

builder.Services.AddDbContext<MyDbContext>(options =>
    options.UseMySql(
        connectionString,
        serverVersion,
        // THIS BLOCK PREVENTS THE CRASH:
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null
        )
    )
    .UseLazyLoadingProxies() // Enable lazy loading
);
// Add other dependencies needed
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>();
builder.Services.AddScoped<IMlService, MlService>();
builder.Services.AddScoped<MissionService>();
builder.Services.AddSingleton<IUserIdProvider, CustomUser>();
builder.Services.AddScoped<IBlobService, BlobService>();

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

builder.Services.AddAuthentication(options =>
{
    // Cookies are the primary for the Web project
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/User/Login";
    options.AccessDeniedPath = "/User/Login";
    options.Cookie.Name = "PocketreeAuthCookie";
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "")),
        NameClaimType = ClaimTypes.Name
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.Request.Path;
            // If the request is for the hub, grab the token from query
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/notificatificationPath"))
            {
                context.Token = accessToken;
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// ✅ CRITICAL FIX: Initialize database BEFORE app.Run() (skip during integration tests)
if (!app.Environment.IsEnvironment("Testing"))
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            
            // 1. Apply migrations first
            Console.WriteLine("Applying database migrations...");
            await db.Database.MigrateAsync();
            
            // 2. Then seed data
            Console.WriteLine("Seeding database...");
            await initDB(app.Services);
        }
        
        Console.WriteLine("✅ Database initialized successfully");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ FATAL: Database initialization failed: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
        
        // In production, fail fast - don't start with broken database
        if (!app.Environment.IsDevelopment())
        {
            throw;
        }
        
        Console.WriteLine("⚠️ Continuing in development mode with uninitialized database");
    }
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection(); // Only in production
}

app.MapHub<MapHub>("/mapHub");
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseRouting();

// Map the Hub route
app.MapHub<NotificationHub>("/notificationHub");

app.UseAuthentication(); 
app.UseAuthorization();
app.UseSession(); // Enable Session middleware

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

async System.Threading.Tasks.Task initDB(IServiceProvider services)
{
    // create the environment to retrieve our database context
    using var scope = services.CreateScope();
    {
        // get database context from DI-container
        var ctx = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        // DO NOT use EnsureCreatedAsync() - it conflicts with migrations!
        // Migrations are already applied above with db.Database.Migrate()

        if (!ctx.Levels.Any())
        {
            // Add Levels
            ctx.Levels.AddRange(
            new Level { LevelID = 1, LevelName = "Seedling", MinCoins = 0, LevelImageURL = "images/levels/seedling.png" },
            new Level { LevelID = 2, LevelName = "Sapling", MinCoins = 250, LevelImageURL = "images/levels/sapling.png" },
            new Level { LevelID = 3, LevelName = "Mighty Oak", MinCoins = 500, LevelImageURL = "images/levels/tree.png" }
            );
        }

        // ctx.Tasks.ExecuteDelete(); // Enable only to clear existing old tasks that need to be replaced by the new list of tasks

        if (!ctx.Tasks.Any())
        {
            // Add Tasks from Tasks.json
            var jsonData = await File.ReadAllTextAsync("tasks.json");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var tasks = JsonSerializer.Deserialize<List<Task>>(jsonData, options);
            
            if (tasks != null)
            {
                ctx.Tasks.AddRange(tasks);
            }
        }

        await ctx.SaveChangesAsync();

        if (!ctx.Badges.Any())
        {
            // Add Badges
            ctx.Badges.AddRange(
                new Badge { BadgeID = 1, BadgeName = "Tree Starter", Description = "This badge is awarded to player who reaches Level 2", BadgeImageURL = "images/badges/tree_starter_badge.png", CriteriaType = "LevelUp", RequiredDifficulty = "Any", RequiredCount = 2 },
                new Badge { BadgeID = 2, BadgeName = "Mighty Oak", Description = "This badge is awarded to player who reaches Level 3", BadgeImageURL = "images/badges/mighty_oak_badge.png", CriteriaType = "LevelUp", RequiredDifficulty = "Any", RequiredCount = 3 },
                new Badge { BadgeID = 3, BadgeName = "Green Starter", Description = "This badge is awarded to player who completed 30 Easy tasks", BadgeImageURL = "images/badges/green_starter_badge.png", CriteriaType = "TaskCount", RequiredDifficulty = "Easy", RequiredCount = 30 },
                new Badge { BadgeID = 4, BadgeName = "Green Champion", Description = "This badge is awarded to player who completed 20 Normal tasks", BadgeImageURL = "images/badges/green_champion_badge.png", CriteriaType = "TaskCount", RequiredDifficulty = "Normal", RequiredCount = 20 },
                new Badge { BadgeID = 5, BadgeName = "Eco Warrior", Description = "This badge is awarded to player who completed 10 Hard tasks", BadgeImageURL = "images/badges/eco_warrior_badge.png", CriteriaType = "TaskCount", RequiredDifficulty = "Hard", RequiredCount = 10 }
            );
        }

        if (!ctx.Skins.Any())
        {
            // Add Skins
            ctx.Skins.AddRange(
                new Skin { SkinID = 1, SkinName = "Animals", SkinPrice = 50, ImageURL = "images/redeem/redeem_skin_animals.png", SkinKey = "Animals" },
                new Skin { SkinID = 2, SkinName = "Hat", SkinPrice = 30, ImageURL = "images/redeem/redeem_skin_hat.png", SkinKey = "Hat" },
                new Skin { SkinID = 3, SkinName = "Sparkles", SkinPrice = 20, ImageURL = "images/redeem/redeem_skin_sparkles.png", SkinKey = "Sparkles" }
            );
        }

        if (!ctx.Vouchers.Any())
        {
            // Add Vouchers
            ctx.Vouchers.AddRange(
                new Voucher { VoucherID = 1, VoucherName = "Voucher 1", Description ="Earned by levelling up to Sapling", MinRedemptionLevel=2 },
                new Voucher { VoucherID = 2, VoucherName = "Voucher 2", Description ="Earned by levelling up to Mighty Oak", MinRedemptionLevel=3 }
            );
        }

        if (!ctx.Users.Any())
        {
            // Add initial Test User 
            ctx.Users.AddRange(
                new User {
                    UserID = 1,
                    Username = "ecotester",
                    PasswordHash = "AQAAAAIAAYagAAAAEMO7BqP3P6mwKCn+y4U448SilNgQsmcaKZlFou2pu3x/3EiFixI8pLMryKFzJWQbOA==", 
                    TotalCoins = 0,
                    CurrentLevelID = 1,
                    LastLoginDate = DateTime.UtcNow,
                    LastActivityDate = DateTime.UtcNow,
                    Email = "ecotester@gmail.com",
                    UserRole = "Player",
                    ResetExpiry = DateTime.UtcNow.AddDays(30),
                    IsOnline = false
                },
                new User
                {
                    UserID = 2,
                    Username = "ecoadmin",
                    PasswordHash = "AQAAAAIAAYagAAAAEMO7BqP3P6mwKCn+y4U448SilNgQsmcaKZlFou2pu3x/3EiFixI8pLMryKFzJWQbOA==",
                    TotalCoins = 0,
                    CurrentLevelID = 1,
                    LastLoginDate = DateTime.UtcNow,
                    LastActivityDate = null,
                    Email = "ecoadmin@gmail.com",
                    UserRole = "Admin",
                    ResetExpiry = DateTime.UtcNow.AddDays(30),
                    IsOnline = false
                });
        }

        if (!ctx.UserPreferences.Any())
        {
            // Add UserPreferences
            ctx.UserPreferences.AddRange(
                new UserPreference { PreferenceID = 1, UserID = 1, PreferredCategory = "Recycling", PreferredDifficulty = "Easy"}
            );
        }

        if (!ctx.Trees.Any())
        {
            ctx.Trees.AddRange(
                new Tree { TreeID = 1, UserID = 1, MissionID = 1, IsCompleted = false, IsWithered = false }
            );
        }

        await ctx.SaveChangesAsync();
    }
}
public partial class Program { }

// Azure deployment configured
