using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ADproject.Models.Entities;
using ADproject.Services;
using System.Diagnostics;

namespace ADproject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly MyDbContext _dbContext;
        private readonly IMlService _mlService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            MyDbContext dbContext,
            IMlService mlService,
            IConfiguration configuration,
            ILogger<HealthController> logger)
        {
            _dbContext = dbContext;
            _mlService = mlService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Basic liveness probe - checks if the API is running
        /// </summary>
        [HttpGet("live")]
        public IActionResult GetLiveness()
        {
            return Ok(new
            {
                status = "Healthy",
                timestamp = DateTime.UtcNow,
                service = "Pocketree API"
            });
        }

        /// <summary>
        /// Readiness probe - checks if API can handle requests
        /// </summary>
        [HttpGet("ready")]
        public async Task<IActionResult> GetReadiness()
        {
            var healthChecks = new Dictionary<string, object>();
            var isHealthy = true;

            // Check Database
            var dbHealth = await CheckDatabaseHealth();
            healthChecks["database"] = dbHealth;
            if (dbHealth.status != "Healthy") isHealthy = false;

            // Check ML Service
            var mlHealth = await CheckMlServiceHealth();
            healthChecks["mlService"] = mlHealth;
            // ML Service is optional - allow "Degraded" status
            if (mlHealth.status != "Healthy" && mlHealth.status != "Degraded") isHealthy = false;

            // Check SignalR Hub (basic check)
            healthChecks["signalR"] = new
            {
                status = "Healthy",
                hubPath = "/mapHub"
            };

            var response = new
            {
                status = isHealthy ? "Healthy" : "Unhealthy",
                timestamp = DateTime.UtcNow,
                checks = healthChecks
            };

            return isHealthy ? Ok(response) : StatusCode(503, response);
        }

        /// <summary>
        /// Detailed health check with all dependencies
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHealth()
        {
            var stopwatch = Stopwatch.StartNew();
            var healthChecks = new Dictionary<string, object>();
            var isHealthy = true;

            // Check Database
            var dbHealth = await CheckDatabaseHealth();
            healthChecks["database"] = dbHealth;
            if (dbHealth.status != "Healthy") isHealthy = false;

            // Check ML Service
            var mlHealth = await CheckMlServiceHealth();
            healthChecks["mlService"] = mlHealth;
            // ML Service is optional - allow "Degraded" status
            if (mlHealth.status != "Healthy" && mlHealth.status != "Degraded") isHealthy = false;

            // Check SignalR Hub
            healthChecks["signalR"] = new
            {
                status = "Healthy",
                hubPath = "/mapHub",
                description = "SignalR MapHub is configured"
            };

            // Check Configuration
            var configHealth = CheckConfiguration();
            healthChecks["configuration"] = configHealth;
            if (configHealth.status != "Healthy") isHealthy = false;

            stopwatch.Stop();

            var response = new
            {
                status = isHealthy ? "Healthy" : "Unhealthy",
                timestamp = DateTime.UtcNow,
                responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                version = "1.0.0",
                environment = _configuration["ASPNETCORE_ENVIRONMENT"] ?? "Development",
                checks = healthChecks
            };

            return isHealthy ? Ok(response) : StatusCode(503, response);
        }

        private async Task<dynamic> CheckDatabaseHealth()
        {
            try
            {
                var timeout = _configuration.GetValue<int>("HealthChecks:DbTimeoutSeconds", 5);
                var stopwatch = Stopwatch.StartNew();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                
                // Try to execute a simple query
                var canConnect = await _dbContext.Database.CanConnectAsync(cts.Token);
                
                if (canConnect)
                {
                    // Get record count from a table
                    var userCount = await _dbContext.Users.CountAsync(cts.Token);
                    stopwatch.Stop();

                    return new
                    {
                        status = "Healthy",
                        responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                        connectionString = MaskConnectionString(_configuration.GetConnectionString("DefaultConnection")),
                        userCount = userCount
                    };
                }
                else
                {
                    stopwatch.Stop();
                    return new
                    {
                        status = "Unhealthy",
                        responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                        error = "Cannot connect to database"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return new
                {
                    status = "Unhealthy",
                    error = ex.Message,
                    type = ex.GetType().Name
                };
            }
        }

        private async Task<dynamic> CheckMlServiceHealth()
        {
            try
            {
                var timeout = _configuration.GetValue<int>("HealthChecks:MlTimeoutSeconds", 5);
                var stopwatch = Stopwatch.StartNew();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
                
                // Simple HTTP check to ML service
                var mlServiceUrl = _configuration["ML_SERVICE_URL"] ?? "http://localhost:5000";
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
                
                var response = await httpClient.GetAsync(mlServiceUrl, cts.Token);
                stopwatch.Stop();

                return new
                {
                    status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy",
                    responseTime = $"{stopwatch.ElapsedMilliseconds}ms",
                    url = mlServiceUrl,
                    statusCode = (int)response.StatusCode
                };
            }
            catch (TaskCanceledException)
            {
                return new
                {
                    status = "Unhealthy",
                    error = "ML Service request timed out"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML Service health check failed");
                return new
                {
                    status = "Degraded",
                    error = ex.Message,
                    type = ex.GetType().Name,
                    note = "API can function with reduced features"
                };
            }
        }

        private dynamic CheckConfiguration()
        {
            var missingConfigs = new List<string>();

            // Check required configurations
            if (string.IsNullOrEmpty(_configuration.GetConnectionString("DefaultConnection")))
                missingConfigs.Add("ConnectionStrings:DefaultConnection");

            if (string.IsNullOrEmpty(_configuration["Jwt:Key"]))
                missingConfigs.Add("Jwt:Key");

            if (string.IsNullOrEmpty(_configuration["Jwt:Issuer"]))
                missingConfigs.Add("Jwt:Issuer");

            if (string.IsNullOrEmpty(_configuration["Jwt:Audience"]))
                missingConfigs.Add("Jwt:Audience");

            if (missingConfigs.Any())
            {
                return new
                {
                    status = "Unhealthy",
                    missingConfigurations = missingConfigs
                };
            }

            return new
            {
                status = "Healthy",
                message = "All required configurations are present"
            };
        }

        private string MaskConnectionString(string? connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not configured";

            // Mask password in connection string
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"password=([^;]+)",
                "password=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
        }
    }
}
