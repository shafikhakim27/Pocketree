namespace Pocketree.Api.Services
{
    public interface IBlobService
    {
        Task<string> UploadFileAsync(string fileName, Stream content);
    }
}
