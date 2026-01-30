namespace Pocketree.Shared.Models;

/// <summary>
/// Represents the result of an operation with success/failure state
/// </summary>
public class Result
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();

    public static Result Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    public static Result Fail(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };

    public static Result Fail(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Represents the result of an operation with a return value
/// </summary>
public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public new static Result<T> Fail(string error) => new()
    {
        Success = false,
        Errors = new List<string> { error }
    };

    public new static Result<T> Fail(IEnumerable<string> errors) => new()
    {
        Success = false,
        Errors = errors.ToList()
    };
}
