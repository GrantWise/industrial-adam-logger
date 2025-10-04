using System.Text.Json;

namespace Industrial.Adam.Logger.WebApi.Authentication;

/// <summary>
/// Industrial-grade file-based API key validator with hot-reload capability.
/// Reads API keys from a JSON configuration file and monitors for changes.
/// </summary>
public class FileBasedApiKeyValidator : IApiKeyValidator, IAsyncDisposable, IDisposable
{
    private readonly ILogger<FileBasedApiKeyValidator> _logger;
    private readonly SemaphoreSlim _keysLock = new(1, 1);
    private readonly FileSystemWatcher _fileWatcher;
    private readonly Timer _debounceTimer;
    private readonly string _keysFilePath;
    private List<ApiKeyInfo> _keys;
    private volatile int _reloadPending = 0;
    private volatile bool _disposed;

    /// <summary>
    /// Initialize the API key validator with hot-reload capability
    /// </summary>
    /// <param name="config">Application configuration</param>
    /// <param name="logger">Logger instance</param>
    public FileBasedApiKeyValidator(IConfiguration config, ILogger<FileBasedApiKeyValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _keysFilePath = config["ApiKeys:FilePath"] ?? "config/apikeys.json";

        // Validate file path to prevent path traversal attacks
        ValidateFilePath(_keysFilePath);

        // Initial load of keys
        _keys = new List<ApiKeyInfo>();
        LoadKeys();

        // Setup FileSystemWatcher for hot-reload
        var directory = Path.GetDirectoryName(Path.GetFullPath(_keysFilePath)) ?? Directory.GetCurrentDirectory();
        var fileName = Path.GetFileName(_keysFilePath);

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Error += OnFileWatcherError;

        // Debounce timer (file system events can fire multiple times)
        _debounceTimer = new Timer(ProcessReload, null, Timeout.Infinite, Timeout.Infinite);

        _logger.LogInformation(
            "API key validator initialized with hot-reload enabled for {Path}. Watching directory: {Directory}",
            _keysFilePath, directory);
    }

    /// <summary>
    /// Validate an API key using constant-time comparison
    /// </summary>
    /// <param name="apiKey">The API key to validate</param>
    /// <returns>API key information if valid, null otherwise</returns>
    /// <remarks>
    /// This implementation uses constant-time comparison to prevent timing attacks.
    /// The method acquires a read lock to ensure thread-safe access during hot-reload operations.
    /// </remarks>
    public async Task<ApiKeyInfo?> ValidateAsync(string apiKey)
    {
        if (_disposed)
            return null;

        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        await _keysLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var key = _keys.FirstOrDefault(k =>
                ConstantTimeEquals(k.Key, apiKey) &&
                (!k.ExpiresAt.HasValue || k.ExpiresAt.Value > DateTimeOffset.UtcNow));

            return key;
        }
        finally
        {
            _keysLock.Release();
        }
    }

    /// <summary>
    /// Handle file change events (debounced)
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_disposed)
            return;

        // Set reload pending flag using Interlocked (thread-safe)
        if (Interlocked.CompareExchange(ref _reloadPending, 1, 0) == 0)
        {
            // Debounce - wait 500ms before reloading (file system events fire multiple times)
            _debounceTimer.Change(500, Timeout.Infinite);
        }
    }

    /// <summary>
    /// Handle file watcher errors
    /// </summary>
    private void OnFileWatcherError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(e.GetException(), "FileSystemWatcher error for API keys file");
    }

    /// <summary>
    /// Process reload after debounce delay
    /// </summary>
    private void ProcessReload(object? state)
    {
        if (_disposed)
            return;

        Interlocked.Exchange(ref _reloadPending, 0);

        _logger.LogInformation("API keys file changed, reloading...");
        LoadKeys();
    }

    /// <summary>
    /// Load API keys from file with comprehensive error handling
    /// </summary>
    private void LoadKeys()
    {
        _keysLock.Wait(); // Synchronous lock (called from constructor and timer callback)
        try
        {
            if (!File.Exists(_keysFilePath))
            {
                _logger.LogWarning(
                    "API keys file not found at {Path}. Service will not authenticate requests.",
                    _keysFilePath);
                _keys = new List<ApiKeyInfo>();
                return;
            }

            // Read file with specific error handling
            string json;
            try
            {
                json = File.ReadAllText(_keysFilePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex,
                    "Permission denied reading API keys file {Path}. Check file permissions (should be 600 on Unix).",
                    _keysFilePath);
                throw new InvalidOperationException(
                    $"Cannot read API keys file due to permissions: {_keysFilePath}. " +
                    $"Ensure file has correct permissions (600 on Unix, restricted ACL on Windows).", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex,
                    "I/O error reading API keys file {Path}.",
                    _keysFilePath);
                throw new InvalidOperationException(
                    $"Cannot read API keys file: {_keysFilePath}", ex);
            }

            // Deserialize with specific error handling
            ApiKeyConfig? keyConfig;
            try
            {
                keyConfig = JsonSerializer.Deserialize<ApiKeyConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Invalid JSON format in API keys file {Path}. Service will not authenticate requests.",
                    _keysFilePath);
                throw new InvalidOperationException(
                    $"API keys file has invalid JSON format: {_keysFilePath}. " +
                    $"Please validate JSON syntax.", ex);
            }

            if (keyConfig?.Keys == null || keyConfig.Keys.Count == 0)
            {
                _logger.LogWarning(
                    "API keys file is empty or has no keys: {Path}. Service will not authenticate requests.",
                    _keysFilePath);
                _keys = new List<ApiKeyInfo>();
                return;
            }

            // Validate loaded keys
            var validatedKeys = ValidateLoadedKeys(keyConfig.Keys);

            _keys = validatedKeys;
            _logger.LogInformation("Successfully loaded {Count} API keys from {Path}", _keys.Count, _keysFilePath);
        }
        finally
        {
            _keysLock.Release();
        }
    }

    /// <summary>
    /// Validate loaded API keys for security and correctness
    /// </summary>
    private List<ApiKeyInfo> ValidateLoadedKeys(List<ApiKeyInfo> keys)
    {
        var validatedKeys = new List<ApiKeyInfo>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            // Validate ID
            if (string.IsNullOrWhiteSpace(key.Id))
            {
                _logger.LogWarning("Skipping API key with empty ID");
                continue;
            }

            // Check for duplicate IDs
            if (seenIds.Contains(key.Id))
            {
                _logger.LogError(
                    "Duplicate API key ID '{Id}' detected. Skipping duplicate entry.",
                    key.Id);
                continue;
            }
            seenIds.Add(key.Id);

            // Validate key value
            if (string.IsNullOrWhiteSpace(key.Key))
            {
                _logger.LogWarning(
                    "Skipping API key '{Id}' with empty key value",
                    key.Id);
                continue;
            }

            // Check key length (security requirement - minimum 16 characters)
            if (key.Key.Length < 16)
            {
                _logger.LogWarning(
                    "API key '{Id}' is too short ({Length} chars). Minimum 16 characters required for security. Skipping.",
                    key.Id, key.Key.Length);
                continue; // Skip weak keys
            }

            // Check if already expired
            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            {
                _logger.LogInformation(
                    "API key '{Id}' ({Name}) expired at {ExpiresAt}. Skipping.",
                    key.Id, key.Name, key.ExpiresAt.Value);
                continue;
            }

            validatedKeys.Add(key);
        }

        return validatedKeys;
    }

    /// <summary>
    /// Validate file path to prevent path traversal attacks
    /// </summary>
    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("API keys file path cannot be null or empty", nameof(filePath));
        }

        // Check for path traversal characters
        if (filePath.Contains("..") || filePath.Contains("~"))
        {
            throw new ArgumentException(
                $"API keys file path '{filePath}' contains path traversal characters (.., ~)",
                nameof(filePath));
        }

        // Get full paths for validation
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"API keys file path '{filePath}' is invalid: {ex.Message}",
                nameof(filePath), ex);
        }

        // Ensure path is within allowed directory (config/)
        var baseDir = Path.GetFullPath("config");
        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"API keys file path '{filePath}' (resolved to '{fullPath}') is outside allowed directory 'config/'. " +
                $"API keys must be stored in the config directory for security.",
                nameof(filePath));
        }
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks
    /// </summary>
    /// <remarks>
    /// This method takes the same amount of time regardless of where strings differ,
    /// preventing attackers from discovering valid API keys through timing analysis.
    /// </remarks>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null)
        {
            return a == b; // Both null = equal, one null = not equal
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }

    /// <summary>
    /// Asynchronously dispose resources
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Stop file watching
        if (_fileWatcher != null)
        {
            _fileWatcher.Changed -= OnFileChanged;
            _fileWatcher.Error -= OnFileWatcherError;
            _fileWatcher.Dispose();
        }

        // Dispose timers
        if (_debounceTimer != null)
        {
            await _debounceTimer.DisposeAsync().ConfigureAwait(false);
        }

        // Dispose locks
        _keysLock?.Dispose();

        _logger.LogInformation("API key validator disposed");
    }

    /// <summary>
    /// Dispose resources (synchronous fallback)
    /// </summary>
    public void Dispose()
    {
        // Call async version and block (acceptable fallback pattern)
        DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
    }
}

/// <summary>
/// Configuration file format for API keys
/// </summary>
internal record ApiKeyConfig
{
    public required List<ApiKeyInfo> Keys { get; init; }
}
