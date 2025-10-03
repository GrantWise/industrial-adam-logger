using System.Collections;

namespace Industrial.Adam.Logger.Core.Processing;

/// <summary>
/// Memory-efficient circular buffer for storing timestamped readings
/// Used for windowed rate calculations without unbounded memory growth
/// </summary>
/// <typeparam name="T">Type of items to store</typeparam>
public sealed class CircularBuffer<T> : IEnumerable<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _tail;
    private int _count;

    /// <summary>
    /// Initialize circular buffer with specified capacity
    /// </summary>
    /// <param name="capacity">Maximum number of items to store</param>
    public CircularBuffer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than zero", nameof(capacity));

        _buffer = new T[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    /// <summary>
    /// Current number of items in the buffer
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Maximum capacity of the buffer
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Whether the buffer is empty
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _count == 0;
            }
        }
    }

    /// <summary>
    /// Whether the buffer is at full capacity
    /// </summary>
    public bool IsFull
    {
        get
        {
            lock (_lock)
            {
                return _count == _buffer.Length;
            }
        }
    }

    /// <summary>
    /// Add an item to the buffer. If at capacity, overwrites the oldest item.
    /// </summary>
    /// <param name="item">Item to add</param>
    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_tail] = item;
            _tail = (_tail + 1) % _buffer.Length;

            if (_count < _buffer.Length)
            {
                _count++;
            }
            else
            {
                // Buffer is full, move head forward (overwrite oldest)
                _head = (_head + 1) % _buffer.Length;
            }
        }
    }

    /// <summary>
    /// Get the most recent item without removing it
    /// </summary>
    /// <returns>Most recent item</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer is empty</exception>
    public T PeekNewest()
    {
        lock (_lock)
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");

            int newestIndex = (_tail - 1 + _buffer.Length) % _buffer.Length;
            return _buffer[newestIndex];
        }
    }

    /// <summary>
    /// Get the oldest item without removing it
    /// </summary>
    /// <returns>Oldest item</returns>
    /// <exception cref="InvalidOperationException">Thrown if buffer is empty</exception>
    public T PeekOldest()
    {
        lock (_lock)
        {
            if (_count == 0)
                throw new InvalidOperationException("Buffer is empty");

            return _buffer[_head];
        }
    }

    /// <summary>
    /// Get all items in chronological order (oldest first)
    /// </summary>
    /// <returns>Array of items in chronological order</returns>
    public T[] ToArray()
    {
        lock (_lock)
        {
            if (_count == 0)
                return [];

            var result = new T[_count];
            for (int i = 0; i < _count; i++)
            {
                result[i] = _buffer[(_head + i) % _buffer.Length];
            }
            return result;
        }
    }

    /// <summary>
    /// Get items within a specific time window
    /// </summary>
    /// <param name="cutoffTime">Items older than this time are excluded</param>
    /// <param name="timeSelector">Function to extract timestamp from item</param>
    /// <returns>Items within the time window, newest first</returns>
    public List<T> GetItemsWithinWindow<TTime>(TTime cutoffTime, Func<T, TTime> timeSelector)
        where TTime : IComparable<TTime>
    {
        lock (_lock)
        {
            if (_count == 0)
                return [];

            var result = new List<T>();

            // Start from newest and work backwards
            for (int i = 0; i < _count; i++)
            {
                int index = (_tail - 1 - i + _buffer.Length) % _buffer.Length;
                var item = _buffer[index];
                var itemTime = timeSelector(item);

                if (itemTime.CompareTo(cutoffTime) >= 0)
                {
                    result.Add(item);
                }
                else
                {
                    // Items are in chronological order, so we can stop here
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Find the oldest item within the specified time window
    /// </summary>
    /// <param name="cutoffTime">Items older than this time are excluded</param>
    /// <param name="timeSelector">Function to extract timestamp from item</param>
    /// <returns>Oldest item within window, or default if none found</returns>
    public T? FindOldestWithinWindow<TTime>(TTime cutoffTime, Func<T, TTime> timeSelector)
        where TTime : IComparable<TTime>
    {
        var itemsInWindow = GetItemsWithinWindow(cutoffTime, timeSelector);
        return itemsInWindow.Count > 0 ? itemsInWindow[^1] : default; // Last item (oldest)
    }

    /// <summary>
    /// Clear all items from the buffer
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// Get enumerator for iterating items in chronological order (oldest first)
    /// </summary>
    /// <returns>Enumerator</returns>
    public IEnumerator<T> GetEnumerator()
    {
        // Create a snapshot to avoid lock during enumeration
        T[] snapshot = ToArray();
        return ((IEnumerable<T>)snapshot).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a device reading with its timestamp for windowed calculations
/// </summary>
public sealed record TimestampedReading
{
    /// <summary>
    /// Timestamp when the reading was taken
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Raw counter value from device
    /// </summary>
    public required long RawValue { get; init; }

    /// <summary>
    /// Processed value after scaling
    /// </summary>
    public required double ProcessedValue { get; init; }

    /// <summary>
    /// Device identifier
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Channel number
    /// </summary>
    public required int Channel { get; init; }

    /// <summary>
    /// Create from DeviceReading
    /// </summary>
    public static TimestampedReading FromDeviceReading(Models.DeviceReading reading)
    {
        return new TimestampedReading
        {
            Timestamp = reading.Timestamp,
            RawValue = reading.RawValue,
            ProcessedValue = reading.ProcessedValue,
            DeviceId = reading.DeviceId,
            Channel = reading.Channel
        };
    }
}
