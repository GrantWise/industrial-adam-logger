namespace Industrial.Adam.Logger.WebApi.Models;

/// <summary>
/// System configuration response (safe for public exposure)
/// </summary>
public sealed class ConfigurationResponse
{
    /// <summary>
    /// Current environment name (Development, Production, etc.)
    /// </summary>
    public required string Environment { get; init; }

    /// <summary>
    /// Logging level (Debug, Information, Warning, Error, etc.)
    /// </summary>
    public string? LogLevel { get; init; }

    /// <summary>
    /// Whether demo mode is enabled
    /// </summary>
    public required bool DemoMode { get; init; }

    /// <summary>
    /// TimescaleDB configuration (excluding credentials)
    /// </summary>
    public required TimescaleDbConfig TimescaleDb { get; init; }
}

/// <summary>
/// TimescaleDB configuration settings
/// </summary>
public sealed class TimescaleDbConfig
{
    /// <summary>
    /// Database server host
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Database server port
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Database name
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// Table name for counter data
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// Batch size for writes
    /// </summary>
    public required int BatchSize { get; init; }

    /// <summary>
    /// Flush interval in milliseconds
    /// </summary>
    public required int FlushIntervalMs { get; init; }
}
