namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Validates API keys for authentication
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validate an API key
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>API key information if valid, null otherwise</returns>
    Task<ApiKeyInfo?> ValidateAsync(string apiKey);
}

/// <summary>
/// Information about an API key
/// </summary>
public record ApiKeyInfo
{
    /// <summary>
    /// Unique identifier for the API key
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The API key value
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Human-readable name for the API key
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional expiration date
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Optional permissions/scopes for this key
    /// </summary>
    public string[]? Permissions { get; init; }
}
