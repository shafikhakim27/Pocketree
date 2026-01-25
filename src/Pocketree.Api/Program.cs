using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Required for UseMySql

var builder = WebApplication.CreateBuilder(args);

// --- 1. ML Service Configuration ---
// Uses Docker env var if available, otherwise falls back to the container name
string mlUrl = builder.Configuration["ML_SERVICE_URL"] ?? "http://ml-service:8000/";

builder.Services.AddHttpClient("ML_Consultant", client => {
    client.BaseAddress = new Uri(mlUrl);
});

// --- 2. Database Configuration ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

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
builder.Services.AddControllersWithViews();

// Add other dependencies
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>(); 
builder.Services.AddScoped<IMlService, MlService>();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); 
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
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
void initDB() 
{
    using (var scope = app.Services.CreateScope())
    {
        var ctx = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        try 
        {
            // Creates the DB if it doesn't exist
            ctx.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while initializing the database.");
            throw; // Re-throw to ensure container restarts if DB fails
        }
    }
}