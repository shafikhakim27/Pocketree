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
        private readonly string _projectId = "500550710563";
        private readonly string _location = "asia-southeast1";
        private readonly string _endpointId = "7416515991628152832"; // Get this from Vertex AI Console

        public MlService(MyDbContext db)
        {
            this.db = db;
        }

        // ML call - To get 3 recommended tasks based on past user's preferred task difficulty and category, total coins earned and recent top 10 tasks completed 
        public async Task<List<Task>> GetRecommendedTasks(int userId)
        {
            // Get the authenticated client
            var client = await GetClientAsync();

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
                // 2. Initialize the Vertex AI Client
                var clientBuilder = new PredictionServiceClientBuilder
                {
                    Endpoint = $"{_location}-aiplatform.googleapis.com"
                };
                client = await clientBuilder.BuildAsync();

                // Convert C# Anonymous Object to Protobuf Value
                // Vertex AI requires an 'instances' list
                Value instance = ToValue(payload);
                var endpointName = EndpointName.FromProjectLocationEndpoint(_projectId, _location, _endpointId);
                // Create a collection of instances
                var instances = new List<Value> { instance };

                // Create the Request Object
                var request = new PredictRequest
                {
                    EndpointAsEndpointName = endpointName,
                    Instances = { ToValue(payload) } 
                };
                // Send the Request
                PredictResponse response = await client.PredictAsync(request);

                // DEBUG: See if there is anything in the metadata or raw response
                Console.WriteLine($"Total Predictions: {response.Predictions.Count}");
                if (response.Predictions.Count == 0)
                {
                    // This will help you see if the model sent an error message 
                    // inside the response object instead of a result.
                    Console.WriteLine("Raw Response: " + response.ToString());
                }

                // Parse the Response
                // Predictions are returned as a list
                foreach (var prediction in response.Predictions)
                {
                    // Convert the Protobuf Value back to a JSON string, then to our DTO
                    string jsonResponse = JsonFormatter.Default.Format(prediction);
                    var mlResponse = JsonSerializer.Deserialize<MlResponseDto>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (mlResponse?.Tasks != null)
                    {
                        Console.WriteLine($"Received {mlResponse.Tasks.Count} tasks from ML.");

                        foreach (var taskDto in mlResponse.Tasks)
                        {
                            tasksToReturn.Add(new Task
                            {
                                Description = taskDto.Description,
                                Difficulty = taskDto.Difficulty,
                                CoinReward = taskDto.CoinReward,
                                Category = taskDto.Category,
                                SourceType = "ML"
                            });
                        }
                    }

                    // Persist only unique ML-generated tasks to the database for monitoring purpose
                    if (tasksToReturn != null && tasksToReturn.Any())
                    {
                        foreach (var mlTask in tasksToReturn)
                        {
                            bool taskExist = await db.Tasks.AnyAsync(t => t.Description == mlTask.Description);
                            if (!taskExist) db.Tasks.Add(mlTask);
                        }

                        await db.SaveChangesAsync();
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Vertex AI Error: {ex.Message}");
                // Fallback logic here if needed
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

        /*

                    // Receive responses from Python
                    if (response.IsSuccessStatusCode)
                        {
                            // For debugging
                            string rawJson = await response.Content.ReadAsStringAsync();
                            Console.WriteLine($"RAW ML JSON: {rawJson}");

                            // Use the options with PropertyNameCaseInsensitive = true
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, IncludeFields = true };

                            // Deserialize into the wrapper instead of a List
                            var mlResponse = JsonSerializer.Deserialize<MlResponseDto>(rawJson, options);

                            if (mlResponse?.Tasks != null)
                            {
                                tasksToReturn = mlResponse.Tasks.Select(dto => new Task
                                {
                                    Description = dto.Description,
                                    Difficulty = dto.Difficulty ?? "Easy",
                                    CoinReward = dto.CoinReward,
                                    Category = dto.Category ?? "General",
                                    SourceType = "ML",
                                }).ToList();

                                // Persist only unique ML-generated tasks to the database for monitoring purpose
                                if (tasksToReturn != null && tasksToReturn.Any())
                                {
                                    foreach (var mlTask in tasksToReturn)
                                    {
                                        bool taskExist = await db.Tasks.AnyAsync(t => t.Description == mlTask.Description);
                                        if (!taskExist) db.Tasks.Add(mlTask);
                                    }

                                    await db.SaveChangesAsync();
                                }
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

                        // Get random Default tasks from the repository to fill up the remaining shortage
                        var fallbackTasks = await db.Tasks
                                                .Where(t => t.SourceType == "Default" && !existingDescriptions.Contains(t.Description))
                                                .OrderBy(t => EF.Functions.Random())
                                                .Take(required)
                                                .ToListAsync();
                        tasksToReturn.AddRange(fallbackTasks);
                    }

                    return tasksToReturn.Take(3).ToList(); // return only 3 tasks as required
        */


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

        private async Task<PredictionServiceClient> GetClientAsync()
        {
            string jsonCredentials = Environment.GetEnvironmentVariable("GOOGLE_CONFIG_JSON");

            if (!string.IsNullOrEmpty(jsonCredentials))
            {
                // Production: Using the Azure JSON String
                var builder = new PredictionServiceClientBuilder
                {
                    Endpoint = "asia-southeast1-aiplatform.googleapis.com",
                    ChannelCredentials = GoogleCredential.FromJson(jsonCredentials)
                        .CreateScoped(PredictionServiceClient.DefaultScopes)
                        .ToChannelCredentials() // Now works thanks to 'using Grpc.Auth'
                };
                return await builder.BuildAsync();
            }
            else
            {
                // Local: Using the vertex-key.json file path
                return await PredictionServiceClient.CreateAsync();
            }
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
    }
}



