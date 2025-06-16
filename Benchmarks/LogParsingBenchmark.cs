using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Log_Parser_App.Benchmarks
{
    /// <summary>
    /// Performance benchmarks for optimized log parsing components
    /// Tests streaming vs traditional parsing and object pooling
    /// </summary>
    [Config(typeof(BenchmarkConfig))]
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net90)]
    public class LogParsingBenchmark
    {
        private IAsyncLogParser _asyncLogParser = null!;
        private ILogEntryPool _logEntryPool = null!;
        private ICacheService<string, LogEntry> _cacheService = null!;
        private string _testFilePath = null!;
        private List<LogEntry> _testEntries = null!;

        [GlobalSetup]
        public void Setup()
        {
            var logger = NullLogger<AsyncLogParser>.Instance;
            var poolLogger = NullLogger<LogEntryPool>.Instance;
            var cacheLogger = NullLogger<CacheService<string, LogEntry>>.Instance;
            
            _logEntryPool = new LogEntryPool(poolLogger);
            _asyncLogParser = new AsyncLogParser(logger, _logEntryPool);
            _cacheService = new CacheService<string, LogEntry>(cacheLogger);

            // Create test file with sample log entries
            _testFilePath = Path.GetTempFileName();
            CreateTestLogFile(_testFilePath, 10000);

            // Create test entries for cache testing
            _testEntries = CreateTestEntries(1000);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
            
            (_logEntryPool as IDisposable)?.Dispose();
            (_cacheService as IDisposable)?.Dispose();
        }

        [Benchmark(Baseline = true)]
        [Arguments(1000)]
        [Arguments(5000)]
        [Arguments(10000)]
        public async Task<int> TraditionalParsing(int entryCount)
        {
            var entries = new List<LogEntry>();
            var lines = await File.ReadAllLinesAsync(_testFilePath);
            
            foreach (var line in lines.Take(entryCount))
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Message = line,
                    Source = "Test",
                    LineNumber = entries.Count + 1
                };
                entries.Add(entry);
            }
            
            return entries.Count;
        }

        [Benchmark]
        [Arguments(1000)]
        [Arguments(5000)]
        [Arguments(10000)]
        public async Task<int> StreamingParsing(int entryCount)
        {
            var count = 0;
            await foreach (var entry in _asyncLogParser.ParseAsync(_testFilePath))
            {
                count++;
                if (count >= entryCount) break;
            }
            return count;
        }

        [Benchmark]
        [Arguments(1000)]
        [Arguments(5000)]
        [Arguments(10000)]
        public async Task<int> StreamingWithPooling(int entryCount)
        {
            var count = 0;
            await foreach (var entry in _asyncLogParser.ParseAsync(_testFilePath))
            {
                var pooledEntry = _logEntryPool.Get();
                pooledEntry.Timestamp = entry.Timestamp;
                pooledEntry.Level = entry.Level;
                pooledEntry.Message = entry.Message;
                pooledEntry.Source = entry.Source;
                pooledEntry.LineNumber = entry.LineNumber;
                
                _logEntryPool.Return(pooledEntry);
                count++;
                if (count >= entryCount) break;
            }
            return count;
        }

        [Benchmark]
        [Arguments(1000)]
        [Arguments(5000)]
        public int CachePerformance(int operationCount)
        {
            var operations = 0;
            var random = new Random(42);

            for (int i = 0; i < operationCount; i++)
            {
                var key = $"entry_{random.Next(0, operationCount / 2)}";
                
                if (random.NextDouble() > 0.3) // 70% cache hits
                {
                    var cached = _cacheService.Get(key);
                    if (cached == null)
                    {
                        var entry = _testEntries[random.Next(_testEntries.Count)];
                        _cacheService.Set(key, entry, TimeSpan.FromMinutes(5));
                    }
                }
                else
                {
                    _cacheService.Invalidate(key);
                }
                operations++;
            }

            return operations;
        }

        [Benchmark]
        public void ObjectPoolingPerformance()
        {
            const int iterations = 10000;
            
            for (int i = 0; i < iterations; i++)
            {
                var entry = _logEntryPool.Get();
                entry.Message = $"Test message {i}";
                entry.Timestamp = DateTime.Now;
                entry.Level = "INFO";
                _logEntryPool.Return(entry);
            }
        }

        [Benchmark]
        public void TraditionalObjectCreation()
        {
            const int iterations = 10000;
            
            for (int i = 0; i < iterations; i++)
            {
                var entry = new LogEntry
                {
                    Message = $"Test message {i}",
                    Timestamp = DateTime.Now,
                    Level = "INFO"
                };
                // Simulate object disposal
                entry = null;
            }
            
            GC.Collect();
        }

        private void CreateTestLogFile(string filePath, int entryCount)
        {
            var lines = new List<string>();
            var random = new Random(42);
            var levels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };
            var sources = new[] { "WebServer", "Database", "Cache", "Auth" };

            for (int i = 0; i < entryCount; i++)
            {
                var timestamp = DateTime.Now.AddMinutes(-random.Next(0, 1440));
                var level = levels[random.Next(levels.Length)];
                var source = sources[random.Next(sources.Length)];
                var message = $"Sample log message {i} with some additional content for realistic size";
                
                lines.Add($"{timestamp:yyyy-MM-dd HH:mm:ss.fff} [{level}] {source}: {message}");
            }

            File.WriteAllLines(filePath, lines);
        }

        private List<LogEntry> CreateTestEntries(int count)
        {
            var entries = new List<LogEntry>();
            var random = new Random(42);
            var levels = new[] { "INFO", "WARN", "ERROR", "DEBUG" };

            for (int i = 0; i < count; i++)
            {
                entries.Add(new LogEntry
                {
                    Timestamp = DateTime.Now.AddMinutes(-random.Next(0, 1440)),
                    Level = levels[random.Next(levels.Length)],
                    Message = $"Test entry {i} with sample content",
                    Source = "BenchmarkTest",
                    LineNumber = i + 1
                });
            }

            return entries;
        }
    }

    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance));
        }
    }
} 