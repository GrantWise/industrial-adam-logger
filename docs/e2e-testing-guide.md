# End-to-End Testing Guide

This guide explains how to run the E2E integration tests that validate the complete data flow from ADAM simulators through the logger service to TimescaleDB.

## Overview

The E2E test suite validates:
- ✅ Single simulator connection and data storage
- ✅ Multiple simulators with concurrent data collection
- ✅ Data quality flags (Good, Unavailable) in TimescaleDB
- ✅ Device restart/reconnection scenarios
- ✅ Counter overflow handling with windowed rate calculation
- ✅ 21 CFR Part 11 compliance (transparent unavailable data reporting)

## Prerequisites

### 1. TimescaleDB Running

The E2E tests require TimescaleDB running on `localhost:5433`:

```bash
# Check if Docker container is running
docker ps | grep adam-timescaledb

# If not running, start it
docker compose -f docker/docker-compose.timescaledb.yml up -d
```

**Verify database connection:**
```bash
docker exec adam-timescaledb psql -U adam_user -d adam_counters -c "SELECT version();"
```

### 2. Simulator Built

The tests will start simulators dynamically, but the simulator project must be built first:

```bash
# Build simulator in Release mode for better performance
dotnet build src/Industrial.Adam.Logger.Simulator/Industrial.Adam.Logger.Simulator.csproj --configuration Release
```

### 3. Database Permissions

Ensure the test user has proper permissions (already configured if you followed the main setup):

```bash
docker exec adam-timescaledb psql -U adam_user -d adam_counters -c "GRANT CREATE ON SCHEMA public TO adam_user;"
```

## Running E2E Tests

### Run All E2E Tests

```bash
# From project root
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~EndToEndTests" \
    --verbosity normal
```

### Run Individual Test

```bash
# Single simulator test
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~SingleSimulator_CollectsDataAndStoresInTimescaleDB" \
    --verbosity normal

# Multiple simulators test
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~MultipleSimulators_CollectDataConcurrently" \
    --verbosity normal

# Offline device test (data quality)
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~SimulatorOffline_ReportsUnavailableQuality" \
    --verbosity normal

# Restart test
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~SimulatorRestart_ServiceReconnectsAndResumesCollection" \
    --verbosity normal
```

### Run All Tests (Unit + Integration + E2E)

```bash
dotnet test src/Industrial.Adam.Logger.Core.Tests --verbosity normal
```

## Test Architecture

### Test Fixture (E2ETestFixture)

The `E2ETestFixture` manages simulator lifecycle:
- Locates simulator project automatically
- Starts simulators on demand with unique ports
- Waits for simulator readiness (health check + Modbus TCP)
- Cleans up simulators after tests
- Shared across all E2E tests in the collection

### Test Structure

```csharp
[Collection("E2E")]  // All E2E tests share fixture
public class EndToEndTests : IAsyncLifetime
{
    // Each test:
    // 1. Starts simulator(s) on unique ports
    // 2. Creates and starts logger service
    // 3. Collects data for specified duration
    // 4. Queries TimescaleDB to verify data
    // 5. Cleans up resources
}
```

### Isolation

Each test uses:
- **Unique Modbus ports** (5510-5599 range)
- **Unique API ports** (8090-8099 range)
- **Unique table names** (`counter_data_e2e_{guid}`)
- **Independent simulators** (started/stopped per test)

This ensures tests can run concurrently without conflicts.

## Test Scenarios

### 1. Single Simulator Test

**What it tests:**
- Simulator starts and provides Modbus TCP data
- Logger service connects via Modbus TCP
- Data is processed and stored in TimescaleDB
- Data quality is marked as "Good"
- Windowed rate calculations are present

**Duration:** ~8 seconds (5s data collection + 3s setup/teardown)

### 2. Multiple Simulators Test

**What it tests:**
- 3 simulators running concurrently
- Logger service manages multiple device connections
- Each device tracked independently
- Data segregated by device_id
- All devices report good quality

**Duration:** ~10 seconds (6s data collection + 4s setup/teardown)

### 3. Offline Device Test (Data Quality)

**What it tests:**
- Service attempts to connect to non-existent device
- "Unavailable" readings are created (21 CFR Part 11 compliance)
- No synthetic data is generated
- Transparent reporting of device offline status

**Duration:** ~5 seconds (3s collection + 2s setup/teardown)

### 4. Restart Test (Reconnection)

**What it tests:**
- Service connects to simulator successfully
- Simulator stops (simulates network failure or device restart)
- Service reports "Unavailable" during disconnect
- Simulator restarts on same port
- Service reconnects automatically
- Data collection resumes with "Good" quality

**Duration:** ~12 seconds (3s initial + 2s disconnect + 4s reconnect + 3s teardown)

### 5. Counter Overflow Test

**What it tests:**
- High-speed simulator (600 units/min)
- Counter values increment rapidly
- Windowed rate calculation handles overflow correctly
- Rates remain within reasonable bounds

**Duration:** ~13 seconds (10s collection + 3s setup/teardown)

### 6. Data Processor Quality Test

**What it tests:**
- Data processor preserves "Unavailable" quality
- No rate calculations on unavailable data
- Data integrity principle: never calculate on missing data

**Duration:** ~5 seconds (3s collection + 2s setup/teardown)

## Expected Output

### Successful Run

```
Starting test execution, please wait...
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 52s
```

### Test Details (Verbose)

```
[ 00:00:08.234] EndToEndTests.SingleSimulator_CollectsDataAndStoresInTimescaleDB [PASS]
[ 00:00:10.567] EndToEndTests.MultipleSimulators_CollectDataConcurrently [PASS]
[ 00:00:05.123] EndToEndTests.SimulatorOffline_ReportsUnavailableQuality [PASS]
[ 00:00:12.456] EndToEndTests.SimulatorRestart_ServiceReconnectsAndResumesCollection [PASS]
[ 00:00:13.789] EndToEndTests.CounterOverflow_WindowedRateCalculationHandlesCorrectly [PASS]
[ 00:00:05.234] EndToEndTests.DataProcessor_PreservesUnavailableQuality [PASS]
```

## Troubleshooting

### Issue: "TimescaleDB connection failed"

**Cause:** Database not running or wrong port

**Solution:**
```bash
# Check database status
docker ps | grep adam-timescaledb

# Start database
docker compose -f docker/docker-compose.timescaledb.yml up -d

# Verify connection
docker exec adam-timescaledb psql -U adam_user -d adam_counters -c "\conninfo"
```

### Issue: "Simulator project not found"

**Cause:** Test cannot locate simulator project directory

**Solution:**
```bash
# Verify simulator exists
ls src/Industrial.Adam.Logger.Simulator/Industrial.Adam.Logger.Simulator.csproj

# If missing, check you're running from project root
pwd  # Should show: /home/grant/industrial-adam-logger
```

### Issue: "Simulator failed to start within timeout"

**Cause:** Simulator taking too long to start, or port conflict

**Solution:**
```bash
# Check if ports are in use
lsof -i :5510-5515
lsof -i :8090-8095

# Kill any stray simulators
pkill -f "Industrial.Adam.Logger.Simulator"

# Re-run tests
```

### Issue: "Permission denied for schema public"

**Cause:** Database user lacks CREATE permission

**Solution:**
```bash
# Grant permissions
docker exec adam-timescaledb psql -U adam_user -d adam_counters \
    -c "GRANT CREATE ON SCHEMA public TO adam_user;"
```

### Issue: Test timeout or hangs

**Cause:** Simulator process didn't clean up properly

**Solution:**
```bash
# Force kill all simulators
pkill -9 -f "Industrial.Adam.Logger.Simulator"

# Clean up any dotnet processes
pkill -9 -f "dotnet run.*Simulator"

# Re-run tests
```

## Performance Notes

### Test Duration Targets

- Total E2E suite: ~50-60 seconds (6 tests)
- Per-test average: ~8-10 seconds
- Setup overhead: ~2-3 seconds per test

### Optimization Tips

**For faster iteration during development:**

```bash
# Run only fast tests (< 6 seconds)
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --filter "FullyQualifiedName~(SimulatorOffline|DataProcessor)" \
    --verbosity normal
```

**For CI/CD pipelines:**

```bash
# Run all tests with higher verbosity
dotnet test src/Industrial.Adam.Logger.Core.Tests \
    --verbosity detailed \
    --logger "console;verbosity=detailed"
```

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: E2E Tests

on: [push, pull_request]

jobs:
  e2e-tests:
    runs-on: ubuntu-latest

    services:
      timescaledb:
        image: timescale/timescaledb:latest-pg16
        env:
          POSTGRES_DB: adam_counters
          POSTGRES_USER: adam_user
          POSTGRES_PASSWORD: adam_password
        ports:
          - 5433:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Build simulator
        run: dotnet build src/Industrial.Adam.Logger.Simulator --configuration Release

      - name: Grant database permissions
        run: |
          docker exec ${{ job.services.timescaledb.id }} \
            psql -U adam_user -d adam_counters \
            -c "GRANT CREATE ON SCHEMA public TO adam_user;"

      - name: Run E2E tests
        run: |
          dotnet test src/Industrial.Adam.Logger.Core.Tests \
            --filter "FullyQualifiedName~EndToEndTests" \
            --verbosity normal \
            --logger "trx;LogFileName=e2e-results.trx"

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: e2e-test-results
          path: '**/e2e-results.trx'
```

## Data Validation Queries

After tests run, you can manually inspect the data:

### View Test Tables

```sql
-- List all E2E test tables
SELECT tablename
FROM pg_tables
WHERE tablename LIKE 'counter_data_e2e_%'
ORDER BY tablename;
```

### Inspect Test Data

```sql
-- View data from a specific test table (replace with actual table name)
SELECT
    device_id,
    channel,
    timestamp,
    raw_value,
    processed_value,
    rate,
    CASE quality
        WHEN 0 THEN 'Good'
        WHEN 1 THEN 'Uncertain'
        WHEN 2 THEN 'Bad'
        WHEN 3 THEN 'Unavailable'
    END as quality_status
FROM counter_data_e2e_abc123
ORDER BY timestamp DESC
LIMIT 20;
```

### Quality Distribution

```sql
-- Count readings by quality for a test table
SELECT
    CASE quality
        WHEN 0 THEN 'Good'
        WHEN 1 THEN 'Uncertain'
        WHEN 2 THEN 'Bad'
        WHEN 3 THEN 'Unavailable'
    END as quality_status,
    COUNT(*) as count
FROM counter_data_e2e_abc123
GROUP BY quality
ORDER BY quality;
```

## Summary

The E2E test suite provides comprehensive validation of the entire Industrial ADAM Logger system:

✅ **Real Modbus TCP communication** (not mocked)
✅ **Actual database writes** (not in-memory)
✅ **Complete data flow** (simulator → service → database)
✅ **Industrial-grade scenarios** (reconnection, data quality, overflow)
✅ **21 CFR Part 11 compliance** (transparent unavailable data)
✅ **Fast execution** (~50-60 seconds for all tests)
✅ **Isolated and repeatable** (unique tables and ports)

This gives high confidence that the system works correctly in production-like scenarios.
