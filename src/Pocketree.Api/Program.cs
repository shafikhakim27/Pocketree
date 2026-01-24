using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore; // Required for UseMySql, Retry logic, and Proxies

var builder = WebApplication.CreateBuilder(args);

// 1. Add services to the container.
builder.Services.AddControllersWithViews();

// 2. Add Database Context with "Retry Logic", "Lazy Loading", and "Fixed Version"
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<MyDbContext>(options =>
{
    // Re-adding Lazy Loading
    options.UseLazyLoadingProxies();

    // OPTIMIZATION: We explicitly tell it "MySQL 8.0" instead of asking it to "AutoDetect".
    // This prevents the "CRITICAL ERROR" log at startup because it doesn't need to connect immediately.
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

    options.UseMySql(connectionString, serverVersion,
        mysqlOptions =>
        {
            // The "Patient" logic: waits 5 seconds between tries if DB is offline
            mysqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,               
                maxRetryDelay: TimeSpan.FromSeconds(5), 
                errorNumbersToAdd: null);
        });
});

// 3. Add other dependencies
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>();

// 4. Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// 5. Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Enable Session middleware
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 6. Initialize Database (Safe Version)
initDB(); 

app.Run();

// --- HELPER METHODS ---

void initDB() 
{
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try 
        {
            var ctx = services.GetRequiredService<MyDbContext>();
            
            // This will now use the Retry Logic if the DB is still waking up
            ctx.Database.EnsureCreated(); 
            
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Database connected and initialized successfully.");
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Still waiting for Database... (Application will continue running)");
        }
    }
}