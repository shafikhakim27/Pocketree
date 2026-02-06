using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pocketree.Api.Models.DTOs;
using System;
using System.Net.Http;
using System.Threading.Tasks;
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

        // ML call - To classify and verify the image submitted for task that requires evidence
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

        // ML call - To get 3 recommended tasks based on past user's preferred task difficulty and category, total coins earned and recent top 10 tasks completed 
        public async Task<List<Task>> GetRecommendedTasks(int userId)
        {
            var tasksToReturn = new List<Task>();

            // Get user's preferred task difficulty and category 
            var userPreferences = await GetUserPreferences(userId);

            // Get total score 
            var userScore = await db.Users
                    .Where(u => u.UserID == userId)
                    .Select(u => u.TotalCoins)
                    .FirstOrDefaultAsync();

            // Prepare data for Python
            var payload = new
            {
                preferredDifficulty = userPreferences.Difficulty,
                preferredCategory = userPreferences.Category,
                totalScore = userScore,
                tasks = await GetTop10HistoricalTasks(userId) // Get recent top 10 tasks completed
            };

            try
            {
                // Send data to Python by calling the Python Flask API 
                var response = await _httpClient2.PostAsJsonAsync("predict", payload);
                // Receive responses from Python
                if (response.IsSuccessStatusCode)
                {
                    var recommendedDto = await response.Content.ReadFromJsonAsync<List<RecommendedTasksDto>>();
                    if (recommendedDto != null)
                    {
                        tasksToReturn = recommendedDto.Select(dto => new Task
                        {
                            Description = dto.Description,
                            Difficulty = dto.Difficulty ?? "Easy",
                            CoinReward = dto.CoinReward,
                            Category = dto.Category ?? "General",
                            SourceType = "ML",
                        }).ToList();
                    }
                }
            }
            // Catch the error in case ML is down
            catch (Exception ex) 
            {
                Console.WriteLine($"ML Error: {ex.Message}");
            }

            // Handle any scenario when there are less than 3 tasks returned by the ML
            if (tasksToReturn.Count < 3)
            {
                int required = 3 - tasksToReturn.Count;

                var existingDescriptions = tasksToReturn.Select(t => t.Description).ToList();
                
                // Get random Normal tasks from the repository to fill up the remaining shortage
                var fallbackTasks = await db.Tasks
                                        .Where(t => t.SourceType == "Normal" && !existingDescriptions.Contains(t.Description))
                                        .OrderBy(r => Guid.NewGuid())
                                        .Take(required)
                                        .ToListAsync();
                tasksToReturn.AddRange(fallbackTasks);
            }

            return tasksToReturn.Take(3).ToList(); // return only 3 tasks as required
        }

        // Helper function to obtain the user's preferred difficulty and category for tasks
        private async Task<UserPrefDto> GetUserPreferences(int userId)
        {
            var history = await db.UserTaskHistory
                            .Where(p => p.UserID == userId)
                            .Include(p => p.Task)
                            .ToListAsync();

            if (!history.Any()) return new UserPrefDto { Difficulty = "Easy", Category = "General" };

            var topDifficulty = history
                                .GroupBy(h => h.Task.Difficulty)
                                .OrderByDescending(g => g.Count())
                                .Select(g => g.Key)
                                .FirstOrDefault();

            var topCategory = history
                                .GroupBy(h => h.Task.Category)
                                .OrderByDescending(g => g.Count())
                                .Select(g => g.Key)
                                .FirstOrDefault();

            return new UserPrefDto { Difficulty = topDifficulty, Category = topCategory };
        }

        // Helper function to get the top 10 completed recent tasks by the user
        private async System.Threading.Tasks.Task<List<string>> GetTop10HistoricalTasks(int userId)
        {
            var top10Tasks = await db.UserTaskHistory
                .Where(t => t.UserID == userId)
                .Include(t => t.Task)
                .OrderByDescending(t => t.CompletionDate)
                .Take(10)
                .ToListAsync();

            return top10Tasks
                    .Select(t => t?.Task?.Description)
                    .OfType<string>() // Ensure no null values   
                    .ToList();
        }

        public class MlResult
        {
            public bool Verified { get; set; }
        }
    }
}



