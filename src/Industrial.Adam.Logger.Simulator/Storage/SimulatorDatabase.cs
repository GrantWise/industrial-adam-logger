using Industrial.Adam.Logger.Simulator.Simulation;
using Microsoft.Data.Sqlite;

namespace Industrial.Adam.Logger.Simulator.Storage;

/// <summary>
/// SQLite database for persisting simulator state
/// </summary>
public class SimulatorDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SimulatorDatabase> _logger;
    private SqliteConnection? _connection;

    public SimulatorDatabase(string databasePath, ILogger<SimulatorDatabase> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = $"Data Source={databasePath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        try
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();

            // Create tables if they don't exist
            using var cmd = _connection.CreateCommand();

            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS simulator_state (
                    device_id TEXT NOT NULL,
                    channel_number INTEGER NOT NULL,
                    counter_value INTEGER NOT NULL DEFAULT 0,
                    production_state TEXT,
                    job_size INTEGER,
                    units_produced INTEGER,
                    last_update DATETIME DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (device_id, channel_number)
                );
                
                CREATE TABLE IF NOT EXISTS production_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    device_id TEXT NOT NULL,
                    channel_number INTEGER,
                    event_type TEXT NOT NULL,
                    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                    duration_seconds INTEGER,
                    details TEXT
                );
                
                CREATE INDEX IF NOT EXISTS idx_events_device_time 
                ON production_events(device_id, timestamp);";

            cmd.ExecuteNonQuery();

            _logger.LogInformation("Database initialized at {Path}", _connectionString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database");
            throw;
        }
    }

    /// <summary>
    /// Save channel state
    /// </summary>
    public async Task SaveChannelStateAsync(
        string deviceId,
        int channelNumber,
        uint counterValue,
        ProductionState? productionState = null,
        int? jobSize = null,
        int? unitsProduced = null)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO simulator_state 
                (device_id, channel_number, counter_value, production_state, 
                 job_size, units_produced, last_update)
                VALUES (@deviceId, @channel, @counter, @state, @jobSize, 
                        @unitsProduced, @timestamp)";

            cmd.Parameters.AddWithValue("@deviceId", deviceId);
            cmd.Parameters.AddWithValue("@channel", channelNumber);
            cmd.Parameters.AddWithValue("@counter", (long)counterValue);
            cmd.Parameters.AddWithValue("@state", productionState?.ToString() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@jobSize", jobSize ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@unitsProduced", unitsProduced ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save channel state");
        }
    }

    /// <summary>
    /// Load channel states
    /// </summary>
    public async Task<Dictionary<int, uint>> LoadChannelStatesAsync(string deviceId)
    {
        var states = new Dictionary<int, uint>();

        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT channel_number, counter_value 
                FROM simulator_state 
                WHERE device_id = @deviceId";

            cmd.Parameters.AddWithValue("@deviceId", deviceId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var channel = reader.GetInt32(0);
                var counter = (uint)reader.GetInt64(1);
                states[channel] = counter;
            }

            _logger.LogInformation("Loaded {Count} channel states for {DeviceId}",
                states.Count, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load channel states");
        }

        return states;
    }

    /// <summary>
    /// Record a production event
    /// </summary>
    public async Task RecordEventAsync(
        string deviceId,
        int? channelNumber,
        string eventType,
        int? durationSeconds = null,
        string? details = null)
    {
        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO production_events 
                (device_id, channel_number, event_type, duration_seconds, details)
                VALUES (@deviceId, @channel, @eventType, @duration, @details)";

            cmd.Parameters.AddWithValue("@deviceId", deviceId);
            cmd.Parameters.AddWithValue("@channel", channelNumber ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@eventType", eventType);
            cmd.Parameters.AddWithValue("@duration", durationSeconds ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@details", details ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record event");
        }
    }

    /// <summary>
    /// Get recent events
    /// </summary>
    public async Task<List<ProductionEvent>> GetRecentEventsAsync(string deviceId, int hours = 24)
    {
        var events = new List<ProductionEvent>();

        try
        {
            using var cmd = _connection!.CreateCommand();
            cmd.CommandText = @"
                SELECT id, channel_number, event_type, timestamp, 
                       duration_seconds, details
                FROM production_events
                WHERE device_id = @deviceId 
                  AND timestamp > datetime('now', @hours)
                ORDER BY timestamp DESC";

            cmd.Parameters.AddWithValue("@deviceId", deviceId);
            cmd.Parameters.AddWithValue("@hours", $"-{hours} hours");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                events.Add(new ProductionEvent
                {
                    Id = reader.GetInt32(0),
                    ChannelNumber = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    EventType = reader.GetString(2),
                    Timestamp = reader.GetDateTime(3),
                    DurationSeconds = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Details = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent events");
        }

        return events;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

public class ProductionEvent
{
    public int Id { get; set; }
    public int? ChannelNumber { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Details { get; set; }
}
