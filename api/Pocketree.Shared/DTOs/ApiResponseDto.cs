namespace Pocketree.Shared.DTOs;

/// <summary>
/// API response wrapper for consistent response format
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Error(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };

    public static ApiResponse<T> Error(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Pagination metadata
/// </summary>
public class PaginationInfo
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;
}

/// <summary>
/// Paginated response wrapper
/// </summary>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public PaginationInfo Pagination { get; set; } = new();
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
}
