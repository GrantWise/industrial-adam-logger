using System.ComponentModel.DataAnnotations;

namespace Industrial.Adam.Logger.Core.Configuration;

/// <summary>
/// TimescaleDB connection and write settings
/// </summary>
public class TimescaleSettings
{
    /// <summary>
    /// PostgreSQL/TimescaleDB server host
    /// </summary>
    [Required(ErrorMessage = "TimescaleDB host is required")]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// PostgreSQL/TimescaleDB server port
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 5432;

    /// <summary>
    /// Database name
    /// </summary>
    [Required(ErrorMessage = "Database name is required")]
    public string Database { get; set; } = "adam_counters";

    /// <summary>
    /// Username for database connection
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = "adam_user";

    /// <summary>
    /// Password for database connection
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Table name for storing counter data
    /// </summary>
    [Required(ErrorMessage = "TableName is required")]
    public string TableName { get; set; } = "counter_data";

    /// <summary>
    /// Batch size for writes
    /// </summary>
    [Range(1, 10000, ErrorMessage = "BatchSize must be between 1 and 10,000")]
    public int BatchSize { get; set; } = Constants.DefaultBatchSize;

    /// <summary>
    /// Batch timeout in milliseconds
    /// </summary>
    [Range(100, 60000, ErrorMessage = "BatchTimeoutMs must be between 100ms and 60 seconds")]
    public int BatchTimeoutMs { get; set; } = Constants.DefaultBatchTimeoutMs;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    [Range(5, 300, ErrorMessage = "TimeoutSeconds must be between 5 and 300")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Flush interval in milliseconds for batch writes
    /// </summary>
    [Range(100, 60000, ErrorMessage = "FlushIntervalMs must be between 100ms and 60 seconds")]
    public int FlushIntervalMs { get; set; } = Constants.DefaultFlushIntervalMs;

    /// <summary>
    /// Maximum retry attempts for database operations
    /// </summary>
    [Range(0, 10, ErrorMessage = "MaxRetryAttempts must be between 0 and 10")]
    public int MaxRetryAttempts { get; set; } = Constants.DefaultDatabaseRetryAttempts;

    /// <summary>
    /// Base delay for retry operations in milliseconds
    /// </summary>
    [Range(100, 10000, ErrorMessage = "RetryDelayMs must be between 100ms and 10 seconds")]
    public int RetryDelayMs { get; set; } = Constants.DefaultDatabaseRetryDelayMs;

    /// <summary>
    /// Maximum delay for retry operations in milliseconds
    /// </summary>
    [Range(1000, 120000, ErrorMessage = "MaxRetryDelayMs must be between 1 second and 2 minutes")]
    public int MaxRetryDelayMs { get; set; } = Constants.MaxDatabaseRetryDelayMs;

    /// <summary>
    /// Enable dead letter queue for failed batches
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;

    /// <summary>
    /// Path for dead letter queue storage (null for default)
    /// </summary>
    public string? DeadLetterQueuePath { get; set; }

    /// <summary>
    /// Graceful shutdown timeout in seconds
    /// </summary>
    [Range(5, 300, ErrorMessage = "ShutdownTimeoutSeconds must be between 5 and 300 seconds")]
    public int ShutdownTimeoutSeconds { get; set; } = Constants.DefaultShutdownTimeoutSeconds;

    /// <summary>
    /// Database initialization timeout in seconds
    /// </summary>
    [Range(5, 120, ErrorMessage = "DatabaseInitTimeoutSeconds must be between 5 and 120 seconds")]
    public int? DatabaseInitTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable SSL/TLS for the connection
    /// </summary>
    public bool EnableSsl { get; set; } = false;

    /// <summary>
    /// Maximum number of connections in the pool
    /// </summary>
    [Range(1, 100, ErrorMessage = "MaxPoolSize must be between 1 and 100")]
    public int MaxPoolSize { get; set; } = 20;

    /// <summary>
    /// Minimum number of connections in the pool
    /// </summary>
    [Range(0, 50, ErrorMessage = "MinPoolSize must be between 0 and 50")]
    public int MinPoolSize { get; set; } = 2;

    /// <summary>
    /// Additional tags to add to all measurements
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    /// Get the PostgreSQL connection string
    /// </summary>
    public string GetConnectionString()
    {
        var connectionString = $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};";

        if (EnableSsl)
        {
            connectionString += "SSL Mode=Require;Trust Server Certificate=true;";
        }

        connectionString += $"Maximum Pool Size={MaxPoolSize};Minimum Pool Size={MinPoolSize};";
        connectionString += $"Timeout={TimeoutSeconds};Command Timeout={TimeoutSeconds};";

        return connectionString;
    }

    /// <summary>
    /// Validate TimescaleDB settings
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Host))
        {
            errors.Add("TimescaleDB host cannot be empty. Configure 'AdamLogger:TimescaleDb:Host' in appsettings.json (e.g., 'localhost' or '192.168.1.100')");
        }

        if (Port <= 0 || Port > 65535)
        {
            errors.Add($"Invalid TimescaleDB port: '{Port}'. Must be between 1 and 65535 (default PostgreSQL port is 5432)");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            errors.Add("TimescaleDB database name cannot be empty. Configure 'AdamLogger:TimescaleDb:Database' in appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            errors.Add("TimescaleDB username cannot be empty. Configure 'AdamLogger:TimescaleDb:Username' in appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            errors.Add("TimescaleDB password cannot be empty. Configure 'AdamLogger:TimescaleDb:Password' in appsettings.json");
        }

        if (string.IsNullOrWhiteSpace(TableName))
        {
            errors.Add("TimescaleDB table name cannot be empty. Configure 'AdamLogger:TimescaleDb:TableName' in appsettings.json");
        }

        // Test connection string construction
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                errors.Add("Failed to construct valid TimescaleDB connection string");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error constructing TimescaleDB connection string: {ex.Message}");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
