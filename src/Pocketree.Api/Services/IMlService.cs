namespace ADproject.Services
{
    public interface IMlService
    {
        Task<bool> ClassifyImageAsync(Stream imageStream, string keyword);
    }
}
