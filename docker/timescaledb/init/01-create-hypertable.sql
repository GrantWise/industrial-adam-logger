-- Initialize TimescaleDB extension and create hypertable for counter data
-- This script runs automatically when the container starts

-- Create the TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb CASCADE;

-- Create the counter_data table
CREATE TABLE IF NOT EXISTS counter_data (
    timestamp TIMESTAMPTZ NOT NULL,
    device_id TEXT NOT NULL,
    channel INTEGER NOT NULL,
    raw_value BIGINT NOT NULL,
    processed_value DOUBLE PRECISION,
    rate DOUBLE PRECISION,
    quality TEXT,
    unit TEXT DEFAULT 'counts',
    PRIMARY KEY (timestamp, device_id, channel)
);

-- Convert to hypertable with 1-hour chunks for optimal performance
SELECT create_hypertable('counter_data', 'timestamp', 
    chunk_time_interval => INTERVAL '1 hour',
    if_not_exists => TRUE);

-- Create indexes for common query patterns
CREATE INDEX IF NOT EXISTS idx_counter_data_device_time 
    ON counter_data (device_id, timestamp DESC);

CREATE INDEX IF NOT EXISTS idx_counter_data_channel_time 
    ON counter_data (device_id, channel, timestamp DESC);

-- Set up retention policy (optional - keep data for 1 year)
SELECT add_retention_policy('counter_data', INTERVAL '1 year', if_not_exists => TRUE);

-- Set up compression policy for older data (compress data older than 7 days)
ALTER TABLE counter_data SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'device_id,channel',
    timescaledb.compress_orderby = 'timestamp DESC'
);

SELECT add_compression_policy('counter_data', INTERVAL '7 days', if_not_exists => TRUE);

-- Create continuous aggregate for hourly stats (optional - for better query performance)
CREATE MATERIALIZED VIEW IF NOT EXISTS counter_data_hourly
WITH (timescaledb.continuous) AS
SELECT 
    time_bucket('1 hour', timestamp) AS hour,
    device_id,
    channel,
    COUNT(*) as reading_count,
    AVG(processed_value) as avg_value,
    MIN(processed_value) as min_value,
    MAX(processed_value) as max_value,
    AVG(rate) as avg_rate
FROM counter_data
GROUP BY hour, device_id, channel;

-- Set up refresh policy for the continuous aggregate
SELECT add_continuous_aggregate_policy('counter_data_hourly',
    start_offset => INTERVAL '1 day',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE);

-- Grant necessary permissions
GRANT ALL PRIVILEGES ON DATABASE adam_counters TO adam_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO adam_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO adam_user;
GRANT USAGE ON SCHEMA public TO adam_user;

-- Log initialization completion
DO $$
BEGIN
    RAISE NOTICE 'TimescaleDB initialization completed successfully';
    RAISE NOTICE 'Created hypertable: counter_data';
    RAISE NOTICE 'Configured compression and retention policies';
    RAISE NOTICE 'Created continuous aggregate: counter_data_hourly';
END $$;