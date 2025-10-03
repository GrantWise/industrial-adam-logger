namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// Standard API response for simple operations
/// </summary>
public sealed class ApiResponse
{
    /// <summary>
    /// Success or error message
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Standard API error response
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>
    /// Error message describing what went wrong
    /// </summary>
    public required string Error { get; init; }
}
