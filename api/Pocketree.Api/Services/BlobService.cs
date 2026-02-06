using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Pocketree.Api.Services
{
    public class BlobService : IBlobService
    {
        private readonly string _connectionString;
        private readonly string _containerName = "profile-pictures";

        public BlobService(IConfiguration configuration)
        {
            // Reads from your User Secrets locally or Environment Variables on Azure
            _connectionString = configuration["AzureStorage:ConnectionString"];
        }

        public async Task<string> UploadFileAsync(string fileName, Stream content)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            // Ensure the container exists and is publically readable
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = "image/jpeg" });

            return blobClient.Uri.ToString(); // Returns the public https:// URL
        }
    }
}
