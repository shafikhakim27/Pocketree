using ADproject.Models.DTOs;
using ADproject.Models.Entities;
using ADproject.Services;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.AIPlatform.V1;
using Grpc.Auth;
using Google.Protobuf.WellKnownTypes;
using Google.Protobuf;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pocketree.Api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Task = ADproject.Models.Entities.Task;
using Value = Google.Protobuf.WellKnownTypes.Value;

namespace ADproject.Services
{
    public class MlService : IMlService
    {
        private readonly MyDbContext db;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        // Google Cloud Fields Required
        private readonly string _projectId;
        private readonly string _location;
        private readonly string _endpointId;

        public MlService(MyDbContext db, HttpClient httpClient, IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            this.db = db;
            _httpClient = httpClient;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;

            // Initialize Google Cloud values from your JSON
            _projectId = _configuration["GoogleCloud:ProjectId"];
            _location = _configuration["GoogleCloud:Location"];
            _endpointId = _configuration["GoogleCloud:EndpointId"];
        }

        // ML call - To get 3 recommended tasks based on past user's preferred task difficulty and category, total coins earned and recent top 10 tasks completed 
        public async Task<List<Task>> GetRecommendedTasks(int userId)
        {
            // Setup Configuration & Environment
            var useLocal = _configuration.GetValue<bool>("MLSettings:UseLocalML");

            var tasksToReturn = new List<Task>();

            // Get user's preferred task difficulty and category 
            var userPreferences = await GetUserPreferences(userId);

            // Get user's profile data 
            var userInfo = await db.Users
                    .Where(u => u.UserID == userId)
                    .Select(u => new { u.UserID, u.TotalCoins, u.LastActivityDate, u.NotAttemptedTaskCount, u.FailedVerificationCount })
                    .FirstOrDefaultAsync();

            // Prepare data for Python
            var payload = new Dictionary<string, object>
            {
                { "user_id", userInfo.UserID },
                { "totalScore", userInfo.TotalCoins },
                { "not_attempted", userInfo.NotAttemptedTaskCount },
                { "failed_verifications", userInfo.FailedVerificationCount },
                { "last_activity_date", userInfo.LastActivityDate?.ToString("O") },
                { "preferredCategory", userPreferences.Category },
                { "preferredDifficulty", userPreferences.Difficulty },
                { "tasks", await GetTop10HistoricalTasks(userId) }
            };

            try
            {
                if (useLocal)
                {
                    // --- LOCAL TESTING PATH ---
                    var client = _httpClientFactory.CreateClient();
                    var url = _configuration["MLSettings:LocalMLUrl"];

                    // MIMIC VERTEX AI: Wrap the payload in an 'instances' list
                    var vertexMimic = new
                    {
                        instances = new[] { payload }
                    };

                    var httpResponse = await client.PostAsJsonAsync(url, vertexMimic);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        // 1. Read the raw string first
                        string rawJson = await httpResponse.Content.ReadAsStringAsync();

                        // 2. Print it so you can finally see your hardcoded message
                        Console.WriteLine($"DEBUG ML RESPONSE: {rawJson}");
                        System.Diagnostics.Debug.WriteLine($"DEBUG ML RESPONSE: {rawJson}");

                        // 3. Since we already have the string, use JsonSerializer instead of ReadFromJsonAsync
                        var mlResponse = System.Text.Json.JsonSerializer.Deserialize<MlResponseDto>(rawJson, new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        //var mlResponse = await httpResponse.Content.ReadFromJsonAsync<MlResponseDto>();
                        ProcessMlTasks(mlResponse, tasksToReturn);
                    }
                }
                else
                {
                    // --- VERTEX AI PRODUCTION PATH ---

                    // Load the credential explicitly from your root folder
                    var credentialPath = Path.Combine(Directory.GetCurrentDirectory(), "vertex-key.json");
                    var credential = GoogleCredential.FromFile(credentialPath);

                    // 2. Build the client using the credential object
                    var clientBuilder = new PredictionServiceClientBuilder
                    {
                        Credential = credential
                    };
                    var client = await clientBuilder.BuildAsync();

                    // 3. Build the Endpoint Name
                    var endpointName = EndpointName.FromProjectLocationEndpoint(_projectId, _location, _endpointId);

                    var request = new PredictRequest
                    {
                        EndpointAsEndpointName = endpointName,
                        Instances = { ToValue(payload) }
                    };

                    // 4. Make the call
                    PredictResponse response = await client.PredictAsync(request);

                    foreach (var prediction in response.Predictions)
                    {
                        string jsonResponse = JsonFormatter.Default.Format(prediction);
                        var mlResponse = JsonSerializer.Deserialize<MlResponseDto>(jsonResponse,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        // Ensure this is awaited since we made ProcessMlTasks async earlier
                        await ProcessMlTasks(mlResponse, tasksToReturn);
                    }
                }

                // Persist unique ML tasks to DB (Preserved from your original code)
                if (tasksToReturn.Any(t => t.SourceType == "ML"))
                {
                    foreach (var mlTask in tasksToReturn.Where(t => t.SourceType == "ML"))
                    {
                        var taskExist = await db.Tasks.FirstOrDefaultAsync(t => t.Description == mlTask.Description);
                        if (taskExist == null)
                        {
                            db.Tasks.Add(mlTask);
                            await db.SaveChangesAsync();
                        }
                        else mlTask.TaskID = taskExist.TaskID;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ML Recommendation Error: {ex.Message}");
            }

            // Handle any scenario when there are less than 3 tasks returned by the ML
            if (tasksToReturn.Count < 3)
            {
                int required = 3 - tasksToReturn.Count;

                var existingDescriptions = tasksToReturn.Select(t => t.Description).ToList();

                // Get random Default tasks from the repository to fill up the remaining shortage
                var fallbackTasks = await db.Tasks
                                        .Where(t => t.SourceType == "Default" && !existingDescriptions.Contains(t.Description))
                                        .OrderBy(t => EF.Functions.Random())
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
                .Take(3)
                .ToListAsync();

            return top10Tasks
                    .Select(t => t?.Task?.Description)
                    .OfType<string>() // Ensure no null values   
                    .ToList();
        }

        // HELPER: Converts C# objects to Protobuf Value (Required for Vertex AI)
        private Value ToValue(object obj)
        {
            if (obj == null) return Value.ForNull();

            // Handle Primitives
            if (obj is string s) return Value.ForString(s);
            if (obj is bool b) return Value.ForBool(b);
            if (obj is int i) return Value.ForNumber(i);
            if (obj is double d) return Value.ForNumber(d);
            if (obj is float f) return Value.ForNumber(f);
            if (obj is long l) return Value.ForNumber(l);

            // Handle Dictionaries 
            if (obj is System.Collections.IDictionary dict)
            {
                var structValue = new Struct();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    structValue.Fields[entry.Key.ToString()] = ToValue(entry.Value);
                }
                return Value.ForStruct(structValue);
            }

            // Handle Lists
            if (obj is System.Collections.IEnumerable list && !(obj is string))
            {
                var listValue = new ListValue();
                foreach (var item in list) listValue.Values.Add(ToValue(item));
                return new Value { ListValue = listValue };
            }

            // Handle Objects/Anonymous Types (The "Catch-all" fallback)
            var objStruct = new Struct();
            foreach (var prop in obj.GetType().GetProperties())
            {
                var val = prop.GetValue(obj);
                if (val != null) objStruct.Fields[prop.Name] = ToValue(val);
            }
            return Value.ForStruct(objStruct);
        }

        // Helper method for the main method
        private async System.Threading.Tasks.Task ProcessMlTasks(MlResponseDto mlResponse, List<Task> tasksToReturn)
        {
            // Check if the top-level 'predictions' list exists
            if (mlResponse?.Predictions != null)
            {
                foreach (var prediction in mlResponse.Predictions)
                {
                    // Check if each prediction has a 'tasks' list
                    if (prediction.Tasks != null)
                    {
                        foreach (var taskDto in prediction.Tasks)
                        {
                            var existingTask = await db.Tasks.FirstOrDefaultAsync(t => t.Description == taskDto.Description);
                            if (existingTask == null)
                            {
                                tasksToReturn.Add(new Task
                                {
                                    Description = $"ML: {taskDto.Description}",
                                    Difficulty = taskDto.Difficulty,
                                    CoinReward = taskDto.CoinReward,
                                    Category = taskDto.Category,
                                    SourceType = "ML"
                                });
                            }
                            else
                                tasksToReturn.Add(existingTask);
                        }
                    }
                }
            }
        }
    }
}



