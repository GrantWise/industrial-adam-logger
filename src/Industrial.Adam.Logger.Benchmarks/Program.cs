using BenchmarkDotNet.Running;
using Industrial.Adam.Logger.Benchmarks;

// Run simple benchmarks that don't require external dependencies
BenchmarkRunner.Run<SimpleBenchmarks>();
BenchmarkRunner.Run<CollectionBenchmarks>();
