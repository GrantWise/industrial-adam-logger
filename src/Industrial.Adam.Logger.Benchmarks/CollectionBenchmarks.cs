using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;

namespace Industrial.Adam.Logger.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class CollectionBenchmarks
{
    private ConcurrentDictionary<string, string> _concurrentDict = null!;
    private Dictionary<string, string> _regularDict = null!;
    private readonly object _lockObject = new();
    private List<string> _keys = null!;

    [GlobalSetup]
    public void Setup()
    {
        _concurrentDict = new ConcurrentDictionary<string, string>();
        _regularDict = new Dictionary<string, string>();
        _keys = new List<string>();

        // Add test data
        for (int i = 0; i < 100; i++)
        {
            var key = $"Device{i:000}:Channel{i % 4}";
            var value = $"Value_{i}";
            _keys.Add(key);
            _concurrentDict[key] = value;
            _regularDict[key] = value;
        }
    }

    [Benchmark]
    public void ConcurrentDictionaryLookup()
    {
        for (int i = 0; i < 1000; i++)
        {
            var key = _keys[i % _keys.Count];
            _ = _concurrentDict.TryGetValue(key, out _);
        }
    }

    [Benchmark]
    public void RegularDictionaryWithLock()
    {
        for (int i = 0; i < 1000; i++)
        {
            var key = _keys[i % _keys.Count];
            lock (_lockObject)
            {
                _ = _regularDict.TryGetValue(key, out _);
            }
        }
    }

    [Benchmark]
    public string StringConcatenation()
    {
        var deviceId = "Device001";
        var channel = 1;
        return $"{deviceId}:{channel}";
    }

    [Benchmark]
    public string StringInterpolation()
    {
        var deviceId = "Device001";
        var channel = 1;
        return string.Format("{0}:{1}", deviceId, channel);
    }

    [Benchmark]
    public void ConcurrentQueueOperations()
    {
        var queue = new ConcurrentQueue<int>();

        // Enqueue
        for (int i = 0; i < 100; i++)
        {
            queue.Enqueue(i);
        }

        // Dequeue
        while (queue.TryDequeue(out _))
        {
        }
    }
}
