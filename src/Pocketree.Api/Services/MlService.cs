using ADproject.Services;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace ADproject.Services
{
    public class MlService : IMlService
    {
        private readonly HttpClient _httpClient;
        private readonly string _pythonApiUrl;

        public MlService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // URL set in appsettings.json 
            _pythonApiUrl = configuration["MlService:Url"];
        }

        public async Task<bool> ClassifyImageAsync(Stream imageStream, string keyword)
        {
            using var content = new MultipartFormDataContent();

            // Add the image file
            var streamContent = new StreamContent(imageStream);
            content.Add(streamContent, "file", "upload.jpg");

            // Add the keyword for MobileNet comparison
            content.Add(new StringContent(keyword), "keyword");

            // 3. Post to the Python Flask API
            var response = await _httpClient.PostAsync(_pythonApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                // Assuming Python returns { "verified": true }
                var result = await response.Content.ReadFromJsonAsync<MlResult>();
                return result?.Verified ?? false;
            }

            return false;
        }
    }

    public class MlResult
    {
        public bool Verified { get; set; }
    }
}

