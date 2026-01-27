using ADproject.Hubs; 
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Security.Claims;
using System.Text;

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


