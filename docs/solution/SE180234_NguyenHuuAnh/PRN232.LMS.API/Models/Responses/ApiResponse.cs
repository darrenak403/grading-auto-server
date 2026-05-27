namespace PRN232.LMS.API.Models.Responses;

/// <summary>Consistent API response wrapper for all endpoints</summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public object? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Request processed successfully") =>
        new() { Success = true, Message = message, Data = data, Errors = null };

    public static ApiResponse<T> Created(T data, string message = "Resource created successfully") =>
        new() { Success = true, Message = message, Data = data, Errors = null };

    public static ApiResponse<T> NotFound(string message = "Resource not found") =>
        new() { Success = false, Message = message, Data = default, Errors = null };

    public static ApiResponse<T> BadRequest(string message, object? errors = null) =>
        new() { Success = false, Message = message, Data = default, Errors = errors };

    public static ApiResponse<T> ServerError(string message = "Internal server error") =>
        new() { Success = false, Message = message, Data = default, Errors = null };
}
