using System.Text.Json;

namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Simple file-based API key validator for industrial IoT scenarios.
/// Reads API keys from a JSON configuration file.
/// </summary>
public class FileBasedApiKeyValidator : IApiKeyValidator
{
    private readonly ILogger<FileBasedApiKeyValidator> _logger;
    private readonly List<ApiKeyInfo> _keys;

    public FileBasedApiKeyValidator(IConfiguration config, ILogger<FileBasedApiKeyValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var keysFilePath = config["ApiKeys:FilePath"] ?? "config/apikeys.json";

        if (File.Exists(keysFilePath))
        {
            try
            {
                var json = File.ReadAllText(keysFilePath);
                var keyConfig = JsonSerializer.Deserialize<ApiKeyConfig>(json);
                _keys = keyConfig?.Keys ?? new List<ApiKeyInfo>();
                _logger.LogInformation("Loaded {Count} API keys from {Path}", _keys.Count, keysFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load API keys from {Path}", keysFilePath);
                _keys = new List<ApiKeyInfo>();
            }
        }
        else
        {
            _logger.LogWarning("No API keys file found at {Path}. API key authentication disabled.", keysFilePath);
            _keys = new List<ApiKeyInfo>();
        }
    }

    public Task<ApiKeyInfo?> ValidateAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return Task.FromResult<ApiKeyInfo?>(null);

        var key = _keys.FirstOrDefault(k =>
            k.Key == apiKey &&
            (!k.ExpiresAt.HasValue || k.ExpiresAt.Value > DateTimeOffset.UtcNow));

        return Task.FromResult(key);
    }
}

/// <summary>
/// Configuration file format for API keys
/// </summary>
internal record ApiKeyConfig
{
    public required List<ApiKeyInfo> Keys { get; init; }
}
