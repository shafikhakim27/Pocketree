using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add database context dependency
builder.Services.AddDbContext<MyDbContext>();
// Add other dependencies needed
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpClient<IMlService, MlService>();

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session expires after 30 mins
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add the context accessor to use Session in Views
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

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

app.UseAuthorization();

// Enable Session middleware (must be before MapControllerRoute)
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

initDB(); 
app.Run();

// init database
void initDB() 
{
    // create the environment to retrieve our database context
    using (var scope = app.Services.CreateScope())
    {
    // get database context from DI-container
    var ctx = scope.ServiceProvider.GetRequiredService<MyDbContext>();
    if (!ctx.Database.CanConnect())
    ctx.Database.EnsureCreated(); // create database
    }
}


