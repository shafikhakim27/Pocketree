using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using Task = ADproject.Models.Entities.Task;

namespace ADproject.Services
{
    public class MlService : IMlService
    {
        private readonly HttpClient _httpClient1;
        private readonly HttpClient _httpClient2;
        private readonly string _pythonApiUrl;
        private readonly MyDbContext db;

        public MlService(HttpClient httpClient, IConfiguration configuration, MyDbContext db, IHttpClientFactory httpClientFactory)
        {
            _httpClient1 = httpClient;
            // URL set in appsettings.json 
            _pythonApiUrl = configuration["MlService:Url"];
            this.db = db;
            // Named client registered in Program.cs
            _httpClient2 = httpClientFactory.CreateClient("ML_Consultant");
        }

        public async Task<bool> ClassifyImageAsync(Stream imageStream, string keyword)
        {
            using var content = new MultipartFormDataContent();

            // Add the image file
            var streamContent = new StreamContent(imageStream);
            content.Add(streamContent, "file", "upload.jpg");

            // Add the keyword for MobileNet comparison
            content.Add(new StringContent(keyword), "keyword");

            // Post to the Python Flask API
            var response = await _httpClient1.PostAsync(_pythonApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                // Assuming Python returns { "verified": true }
                var result = await response.Content.ReadFromJsonAsync<MlResult>();
                return result?.Verified ?? false;
            }

            return false;
        }

        public async Task<List<Task>> GetRecommendedTasks(int userId)
        {
            // Get user preferences and total score data for Python
            var userPreferences = await db.UserPreferences
                    .Where(p => p.UserID == userId)
                    .ToListAsync();

            var userScore = await db.Users
                    .Where(u => u.UserID == userId)
                    .Select(u => u.TotalCoins)
                    .FirstOrDefaultAsync();

            // Prepare data for Python
            var payload = new
            {
                preferences = userPreferences.Select(i => new { i.PreferredCategory, i.PreferredDifficulty }),
                totalScore = userScore,
                tasks = await db.Tasks.ToListAsync()
            };
              
            // Send data to Python by calling the Python Flask API
            var response = await _httpClient2.PostAsJsonAsync("predict", payload);
            // Receive ranked IDs from Python
            var rankedIds = await response.Content.ReadFromJsonAsync<List<int>>();

            // Fetch full Task objects from MySQL using the IDs
            return await db.Tasks
                .Where(t => rankedIds.Contains(t.TaskID))
                .OrderBy(t => rankedIds.IndexOf(t.TaskID))
                .Take(3) // Return the top 3 matches
                .ToListAsync();
        }
    }

    public class MlResult
    {
        public bool Verified { get; set; }
    }
}



