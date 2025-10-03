# Functional Fix Plan - Industrial ADAM Logger

**Created:** October 3, 2025
**Goal:** Fix critical functional issues to enable proper testing
**Scope:** Non-security issues that affect functionality, data integrity, and reliability

---

## Priority Order (Excluding Security)

### Phase 1: Critical Functional Fixes (Must Fix First)
These issues can cause data loss, corruption, or system instability.

#### 1. **Fix Async Void Event Handler** ⚠️ CRITICAL
**File:** `src/Industrial.Adam.Logger.Core/Services/AdamLoggerService.cs:166`
**Issue:** `async void OnReadingReceived()` can cause unhandled exceptions and silent data loss
**Impact:** Data loss, application crashes, no visibility into failures
**Effort:** 2 hours

**Solution Approach:**
```csharp
// Replace event handler with Channel-based processing
private readonly Channel<DeviceReading> _readingChannel;

// In constructor:
_readingChannel = Channel.CreateUnbounded<DeviceReading>();
_ = Task.Run(ProcessReadingsAsync, _stoppingCts.Token);

// Event handler becomes simple:
private void OnReadingReceived(DeviceReading reading)
{
    _readingChannel.Writer.TryWrite(reading);
}

// Background processor with proper async:
private async Task ProcessReadingsAsync()
{
    await foreach (var reading in _readingChannel.Reader.ReadAllAsync(_stoppingCts.Token))
    {
        try
        {
            var processed = _dataProcessor.ProcessReading(reading, previousReading);
            _lastReadings[channelKey] = processed;
            await _timescaleStorage.WriteReadingAsync(processed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reading");
            // Proper error handling with visibility
        }
    }
}
```

---

#### 2. **Add DataQuality.Unavailable State** ⚠️ CRITICAL
**File:** `src/Industrial.Adam.Logger.Core/Models/DataQuality.cs`
**Issue:** Missing "Unavailable" state violates 21 CFR Part 11 compliance
**Impact:** Compliance violation, unclear data quality
**Effort:** 1 hour

**Solution Approach:**
```csharp
// 1. Update DataQuality enum
public enum DataQuality
{
    Good = 0,
    Degraded = 1,
    Bad = 2,
    Unavailable = 3  // NEW
}

// 2. Update ModbusDevicePool to use Unavailable when device fails
// File: ModbusDevicePool.cs:237-244
if (!result.Success)
{
    _healthTracker.RecordFailure(deviceId, result.Error ?? "Unknown error");

    // Create unavailable reading instead of skipping
    var unavailableReading = new DeviceReading
    {
        DeviceId = deviceId,
        Channel = channel.ChannelNumber,
        RawValue = 0,
        ProcessedValue = 0,
        Timestamp = DateTimeOffset.UtcNow,
        Quality = DataQuality.Unavailable,  // Clear indication
        Unit = channel.Unit
    };

    ReadingReceived?.Invoke(unavailableReading);
}
```

**Files to Update:**
- `DataQuality.cs` - Add enum value
- `ModbusDevicePool.cs` - Use Unavailable on connection failure
- `DataProcessor.cs` - Handle Unavailable quality
- Update any tests that check quality states

---

#### 3. **Fix Race Condition in Device Restart** ⚠️ CRITICAL
**File:** `src/Industrial.Adam.Logger.Core/Devices/ModbusDevicePool.cs:127-152`
**Issue:** Old polling task may still run when new task starts
**Impact:** Duplicate readings, data corruption, resource leaks
**Effort:** 3 hours

**Solution Approach:**
```csharp
// 1. Add tracking fields to DeviceContext
private sealed class DeviceContext : IDisposable
{
    public required ModbusDeviceConnection Connection { get; init; }
    public required DeviceConfig Config { get; init; }
    public CancellationTokenSource CancellationTokenSource { get; set; }
    public Task? PollingTask { get; set; }  // NEW - track the task
    public SemaphoreSlim RestartLock { get; } = new(1, 1);  // NEW - prevent concurrent restarts

    public void Dispose()
    {
        RestartLock?.Dispose();
        CancellationTokenSource?.Dispose();
        Connection?.Dispose();
    }
}

// 2. Fix RestartDeviceAsync with proper synchronization
public async Task<bool> RestartDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
{
    if (!_devices.TryGetValue(deviceId, out var context))
        return false;

    _logger.LogInformation("Restarting device {DeviceId}", deviceId);

    // Prevent concurrent restarts
    await context.RestartLock.WaitAsync(cancellationToken);
    try
    {
        // Cancel old task
        var oldCts = context.CancellationTokenSource;
        await oldCts.CancelAsync();

        // Wait for old task to complete (with timeout)
        if (context.PollingTask != null)
        {
            var completed = await Task.WhenAny(
                context.PollingTask,
                Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)
            ) == context.PollingTask;

            if (!completed)
            {
                _logger.LogWarning("Old polling task did not stop within 5 seconds for {DeviceId}", deviceId);
            }
        }

        // Disconnect
        await context.Connection.DisconnectAsync();

        // Create new CTS and start new task
        context.CancellationTokenSource = new CancellationTokenSource();
        context.PollingTask = Task.Run(
            () => PollDeviceAsync(context),
            context.CancellationTokenSource.Token
        );

        return true;
    }
    finally
    {
        context.RestartLock.Release();
    }
}

// 3. Update AddDeviceAsync to track polling task
context.PollingTask = Task.Run(
    async () => await PollDeviceAsync(context).ConfigureAwait(false),
    context.CancellationTokenSource.Token
);
```

---

### Phase 2: High Priority Functional Fixes

#### 4. **Implement IAsyncDisposable Pattern** 🔴 HIGH
**Files:**
- `AdamLoggerService.cs:280-303`
- `ModbusDevicePool.cs:302-319`
- `TimescaleStorage.cs:609-653`

**Issue:** Blocking async operations in Dispose can cause deadlocks
**Impact:** Application hangs on shutdown
**Effort:** 2 hours

**Solution Approach:**
```csharp
// Implement IAsyncDisposable alongside IDisposable
public sealed class AdamLoggerService : IHostedService, IDisposable, IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _devicePool.ReadingReceived -= OnReadingReceived;

        try
        {
            await StopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during async dispose");
        }

        _stoppingCts?.Dispose();
        _startStopLock?.Dispose();
    }

    public void Dispose()
    {
        // Call async version synchronously (last resort)
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
```

**Apply same pattern to:**
- `ModbusDevicePool`
- `TimescaleStorage`
- `DeadLetterQueue`

---

#### 5. **Fix Blocking Async in GetHealthStatus** 🔴 HIGH
**File:** `src/Industrial.Adam.Logger.Core/Storage/TimescaleStorage.cs:241`
**Issue:** `.GetAwaiter().GetResult()` blocks thread pool under lock
**Impact:** Thread pool starvation, performance degradation
**Effort:** 1 hour

**Solution Approach:**
```csharp
// Option 1: Make async version (preferred)
public async Task<StorageHealthStatus> GetHealthStatusAsync()
{
    var latencies = _batchLatencies.ToArray();
    var avgLatency = latencies.Length > 0 ? latencies.Average() : 0.0;

    var dlqSize = _deadLetterQueue != null
        ? await _deadLetterQueue.GetQueueSizeAsync()
        : 0;

    return new StorageHealthStatus
    {
        DeadLetterQueueSize = dlqSize,
        // ... other fields
    };
}

// Option 2: Cache DLQ size (simpler, faster)
private volatile int _cachedDlqSize = 0;

// Update in background task every 10 seconds
private async Task UpdateCachedMetricsAsync()
{
    while (!_backgroundCts.Token.IsCancellationRequested)
    {
        try
        {
            if (_deadLetterQueue != null)
            {
                _cachedDlqSize = await _deadLetterQueue.GetQueueSizeAsync();
            }
            await Task.Delay(TimeSpan.FromSeconds(10), _backgroundCts.Token);
        }
        catch (OperationCanceledException) { break; }
    }
}

public StorageHealthStatus GetHealthStatus()
{
    return new StorageHealthStatus
    {
        DeadLetterQueueSize = _cachedDlqSize,  // Use cached value
        // ... other fields
    };
}
```

---

#### 6. **Add Circuit Breaker for Database Operations** 🔴 HIGH
**File:** `src/Industrial.Adam.Logger.Core/Storage/TimescaleStorage.cs:106-121`
**Issue:** No circuit breaker means continuous retries on persistent failures
**Impact:** System overwhelm, dead letter queue explosion
**Effort:** 2 hours

**Solution Approach:**
```csharp
// 1. Add Polly.Extensions.Http NuGet package (includes circuit breaker)

// 2. Create circuit breaker policy
private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

public TimescaleStorage(...)
{
    // Circuit breaker: open after 10 failures, stay open for 1 minute
    _circuitBreakerPolicy = Policy
        .Handle<NpgsqlException>()
        .Or<TimeoutException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 10,
            durationOfBreak: TimeSpan.FromMinutes(1),
            onBreak: (ex, duration) =>
            {
                _logger.LogError(ex, "Circuit breaker OPENED for {Duration}", duration);
                lock (_healthLock)
                {
                    _isBackgroundTaskHealthy = false;
                    _lastError = $"Circuit breaker opened: {ex.Message}";
                }
            },
            onReset: () =>
            {
                _logger.LogInformation("Circuit breaker RESET");
                lock (_healthLock)
                {
                    _isBackgroundTaskHealthy = true;
                    _lastError = null;
                }
            },
            onHalfOpen: () =>
            {
                _logger.LogInformation("Circuit breaker HALF-OPEN (testing recovery)");
            }
        );

    // Wrap retry policy with circuit breaker
    _retryPolicy = Policy
        .Handle<NpgsqlException>()
        .Or<TimeoutException>()
        .WaitAndRetryAsync(...)
        .WrapAsync(_circuitBreakerPolicy);
}

// 3. Update WriteBatchToTimescaleAsync to use wrapped policy
await _retryPolicy.ExecuteAsync(async () =>
{
    // ... database write logic
});
```

**Files to Update:**
- Add `Polly.Extensions.Http` package to `Industrial.Adam.Logger.Core.csproj`
- Update `TimescaleStorage.cs` constructor and write methods

---

### Phase 3: Medium Priority Fixes

#### 7. **Add Retry Logic to Dead Letter Queue File I/O** 🟡 MEDIUM
**File:** `src/Industrial.Adam.Logger.Core/Storage/DeadLetterQueue.cs:104-120`
**Issue:** File I/O can fail transiently, no retry mechanism
**Impact:** Data loss from skipped files
**Effort:** 1.5 hours

**Solution Approach:**
```csharp
// Use Polly for file I/O retry
private readonly AsyncRetryPolicy _fileRetryPolicy;

public DeadLetterQueue(...)
{
    _fileRetryPolicy = Policy
        .Handle<IOException>()
        .Or<UnauthorizedAccessException>()
        .WaitAndRetryAsync(
            3,
            attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1)),
            onRetry: (ex, delay, attempt, ctx) =>
            {
                _logger.LogWarning(ex, "File I/O retry {Attempt}/3 after {Delay}ms", attempt, delay.TotalMilliseconds);
            }
        );
}

// Update GetFailedBatchesAsync
foreach (var file in files)
{
    try
    {
        var json = await _fileRetryPolicy.ExecuteAsync(async () =>
            await File.ReadAllTextAsync(file)
        );

        var batch = JsonSerializer.Deserialize<FailedBatch>(json);

        if (batch != null && batch.Readings?.Count > 0)
        {
            failedBatches.Add(batch);
        }
        else
        {
            _logger.LogWarning("Invalid batch in {File}, moving to error folder", file);
            await MoveToErrorFolderAsync(file);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to read {File} after retries", file);
    }
}
```

---

#### 8. **Validate Table Name to Prevent SQL Injection** 🟡 MEDIUM
**File:** `src/Industrial.Adam.Logger.Core/Storage/TimescaleStorage.cs:279`
**Issue:** Table name concatenated into SQL without validation
**Impact:** SQL injection risk if table name becomes dynamic
**Effort:** 30 minutes

**Solution Approach:**
```csharp
// Add validation in constructor
private static readonly Regex ValidTableNameRegex = new(
    @"^[a-z_][a-z0-9_]{0,62}$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase
);

public TimescaleStorage(...)
{
    // Validate table name
    if (!ValidTableNameRegex.IsMatch(settings.TableName))
    {
        throw new ArgumentException(
            $"Invalid table name '{settings.TableName}'. Must be alphanumeric with underscores, max 63 chars.",
            nameof(settings)
        );
    }

    // Use quoted identifier for extra safety
    var createHypertableSql = $"SELECT create_hypertable('public.\"{_settings.TableName}\"', 'timestamp', ...)";
}
```

---

#### 9. **Fix Timer Disposal Race Condition** 🟡 MEDIUM
**File:** `src/Industrial.Adam.Logger.Core/Storage/DeadLetterQueue.cs:229-234`
**Issue:** Timer may fire during disposal
**Impact:** Background task may start after disposal
**Effort:** 30 minutes

**Solution Approach:**
```csharp
private readonly CancellationTokenSource _disposalCts = new();

private void PersistPendingBatches(object? state)
{
    if (_disposalCts.IsCancellationRequested)
        return;

    _ = Task.Run(PersistPendingBatchesAsync, _disposalCts.Token);
}

public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    // Cancel first to stop timer callbacks
    _disposalCts.Cancel();

    // Dispose timer
    _persistenceTimer?.Dispose();

    // Final persistence
    try
    {
        PersistPendingBatchesAsync().GetAwaiter().GetResult();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Error during final persistence");
    }

    _persistenceLock?.Dispose();
    _disposalCts?.Dispose();
}
```

---

#### 10. **Add Timeout to Database Initialization** 🟡 MEDIUM
**File:** `src/Industrial.Adam.Logger.Core/Storage/TimescaleStorage.cs:253-292`
**Issue:** Can hang indefinitely on database connection
**Impact:** Blocks application startup
**Effort:** 30 minutes

**Solution Approach:**
```csharp
private async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
{
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
        cancellationToken,
        timeoutCts.Token
    );

    try
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(linkedCts.Token).ConfigureAwait(false);

        // ... rest of initialization with linkedCts.Token
    }
    catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
    {
        throw new TimeoutException(
            "Database initialization timed out after 30 seconds. Check database connectivity."
        );
    }
}
```

---

## Implementation Order

### Day 1 - Critical Fixes (6-8 hours)
1. ✅ **Add DataQuality.Unavailable** (1 hour) - Simple enum change
2. ✅ **Fix Async Void Event Handler** (2 hours) - Channel-based processing
3. ✅ **Fix Device Restart Race Condition** (3 hours) - Add synchronization

**Testing:** Run integration tests, verify no duplicate readings, check data quality states

### Day 2 - High Priority (5-6 hours)
4. ✅ **Implement IAsyncDisposable** (2 hours) - Proper async disposal
5. ✅ **Fix Blocking GetHealthStatus** (1 hour) - Use cached metrics
6. ✅ **Add Circuit Breaker** (2 hours) - Resilience pattern

**Testing:** Load test with database failures, verify circuit breaker behavior

### Day 3 - Medium Priority (3-4 hours)
7. ✅ **Add DLQ File Retry** (1.5 hours) - Polly retry for files
8. ✅ **Validate Table Name** (30 min) - SQL injection prevention
9. ✅ **Fix Timer Disposal** (30 min) - Cancellation token
10. ✅ **Add DB Init Timeout** (30 min) - Prevent startup hangs

**Testing:** Full integration test suite, chaos testing (disk full, db down, etc.)

---

## Testing Strategy

### Unit Tests to Add/Update
- `DataQuality.Unavailable` handling in DataProcessor
- Circuit breaker state transitions
- Device restart synchronization
- Health status caching

### Integration Tests to Add/Update
- Event handler with heavy load (verify no data loss)
- Device restart during active polling
- Database connection loss and recovery
- Dead letter queue file corruption recovery

### Manual Testing Checklist
- [ ] Start service with database down (verify timeout)
- [ ] Restart device multiple times rapidly (verify no duplicates)
- [ ] Disconnect device during polling (verify Unavailable quality)
- [ ] Fill dead letter queue (verify persistence and recovery)
- [ ] Load test: 1000 readings/second for 5 minutes
- [ ] Graceful shutdown test (verify all data flushed)

---

## Package Dependencies to Add

```xml
<!-- Industrial.Adam.Logger.Core.csproj -->
<ItemGroup>
  <!-- Already has Polly 8.5.0, verify it includes circuit breaker -->
  <!-- If not, add: -->
  <PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
</ItemGroup>
```

---

## Risk Mitigation

### Backup Strategy
1. Create feature branch: `git checkout -b fix/functional-issues`
2. Commit after each fix
3. Run tests before moving to next fix

### Rollback Plan
- Each fix is independent and can be reverted individually
- Keep `master` branch stable
- Tag working states: `git tag -a v2.0.1-functional-fixes`

### Communication
- Update `CHANGELOG.md` with each fix
- Document breaking changes (if any)
- Update `CLAUDE.md` with new patterns

---

## Success Criteria

✅ **All fixes implemented and tested**
✅ **No data loss under load (10,000 readings)**
✅ **Graceful handling of all device/database failures**
✅ **No race conditions in concurrent operations**
✅ **Application starts and stops cleanly**
✅ **Integration tests pass 100%**
✅ **Dead letter queue recovers all failed batches**

**Estimated Total Effort:** 14-18 development hours over 3 days

---

**Next Steps:**
1. Review this plan and prioritize
2. Create GitHub issues for tracking (optional)
3. Start with Phase 1, fix #1 (DataQuality.Unavailable - easiest win)
4. Run tests after each fix before proceeding
