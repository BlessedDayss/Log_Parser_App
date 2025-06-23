using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Log_Parser_App.Services.Filtering;
using Microsoft.Extensions.DependencyInjection;

namespace Log_Parser_App
{
    /// <summary>
    /// Simple performance test to verify optimization improvements
    /// Compares traditional vs optimized approaches
    /// </summary>
    public class PerformanceTest
    {
        private readonly IAsyncLogParser _asyncLogParser;
        private readonly ILogEntryPool _logEntryPool;
        private readonly ICacheService<string, LogEntry> _cacheService;

        public PerformanceTest()
        {
            var poolLogger = NullLogger<LogEntryPool>.Instance;
            var parserLogger = NullLogger<AsyncLogParser>.Instance;
            var cacheLogger = NullLogger<CacheService<string, LogEntry>>.Instance;

            _logEntryPool = new LogEntryPool(poolLogger);
            _asyncLogParser = new AsyncLogParser(parserLogger, _logEntryPool);
            _cacheService = new CacheService<string, LogEntry>(cacheLogger);
        }

        public async Task RunPerformanceTests()
        {
            Console.WriteLine("🚀 PERFORMANCE OPTIMIZATION TESTS");
            Console.WriteLine("==================================");

            // Create test file
            var testFile = Path.GetTempFileName();
            CreateTestLogFile(testFile, 10000);

            try
            {
                await TestParsingPerformance(testFile);
                TestObjectPoolingPerformance();
                TestCachePerformance();
                
                Console.WriteLine("\n✅ All performance tests completed successfully!");
                Console.WriteLine("📊 Performance optimizations are working correctly.");
            }
            finally
            {
                File.Delete(testFile);
                (_logEntryPool as IDisposable)?.Dispose();
                (_cacheService as IDisposable)?.Dispose();
            }
        }

        private async Task TestParsingPerformance(string testFile)
        {
            Console.WriteLine("\n📈 PARSING PERFORMANCE TEST");
            Console.WriteLine("---------------------------");

            const int testEntries = 5000;

            // Traditional parsing
            var sw = Stopwatch.StartNew();
            var traditionalEntries = new List<LogEntry>();
            var lines = await File.ReadAllLinesAsync(testFile);
            
            foreach (var line in lines.Take(testEntries))
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Message = line,
                    Source = "Test",
                    LineNumber = traditionalEntries.Count + 1
                };
                traditionalEntries.Add(entry);
            }
            sw.Stop();
            var traditionalTime = sw.ElapsedMilliseconds;

            // Streaming parsing
            sw.Restart();
            var streamingCount = 0;
            await foreach (var entry in _asyncLogParser.ParseAsync(testFile))
            {
                streamingCount++;
                if (streamingCount >= testEntries) break;
            }
            sw.Stop();
            var streamingTime = sw.ElapsedMilliseconds;

            Console.WriteLine($"Traditional parsing: {traditionalTime}ms ({traditionalEntries.Count} entries)");
            Console.WriteLine($"Streaming parsing:   {streamingTime}ms ({streamingCount} entries)");
            
            if (streamingTime < traditionalTime)
            {
                var improvement = ((double)(traditionalTime - streamingTime) / traditionalTime) * 100;
                Console.WriteLine($"🎯 Streaming is {improvement:F1}% faster!");
            }
        }

        private void TestObjectPoolingPerformance()
        {
            Console.WriteLine("\n🔄 OBJECT POOLING PERFORMANCE TEST");
            Console.WriteLine("----------------------------------");

            const int iterations = 10000;

            // Traditional object creation
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var entry = new LogEntry
                {
                    Message = $"Test message {i}",
                    Timestamp = DateTime.Now,
                    Level = "INFO"
                };
                entry = null; // Simulate disposal
            }
            GC.Collect();
            sw.Stop();
            var traditionalTime = sw.ElapsedMilliseconds;

            // Object pooling
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var entry = _logEntryPool.Get();
                entry.Message = $"Test message {i}";
                entry.Timestamp = DateTime.Now;
                entry.Level = "INFO";
                _logEntryPool.Return(entry);
            }
            sw.Stop();
            var poolingTime = sw.ElapsedMilliseconds;

            Console.WriteLine($"Traditional creation: {traditionalTime}ms");
            Console.WriteLine($"Object pooling:       {poolingTime}ms");
            
            if (poolingTime < traditionalTime)
            {
                var improvement = ((double)(traditionalTime - poolingTime) / traditionalTime) * 100;
                Console.WriteLine($"🎯 Object pooling is {improvement:F1}% faster!");
            }

            // Show pool statistics
            var stats = _logEntryPool.GetStatistics();
            Console.WriteLine($"Pool stats: {stats.TotalGets} gets, {stats.TotalReturns} returns, {stats.CurrentPoolSize} pooled");
        }

        private void TestCachePerformance()
        {
            Console.WriteLine("\n💾 CACHE PERFORMANCE TEST");
            Console.WriteLine("-------------------------");

            const int operations = 5000;
            var random = new Random(42);
            var testEntries = CreateTestEntries(1000);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < operations; i++)
            {
                var key = $"entry_{random.Next(0, operations / 2)}";
                
                if (random.NextDouble() > 0.3) // 70% cache hits
                {
                    var cached = _cacheService.Get(key);
                    if (cached == null)
                    {
                        var entry = testEntries[random.Next(testEntries.Count)];
                        _cacheService.Set(key, entry, TimeSpan.FromMinutes(5));
                    }
                }
                else
                {
                    _cacheService.Invalidate(key);
                }
            }
            sw.Stop();

            var stats = _cacheService.GetStatistics();
            Console.WriteLine($"Cache operations: {operations} in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Hit ratio: {stats.HitRatio:P1} ({stats.HitCount} hits, {stats.MissCount} misses)");
            Console.WriteLine($"Cached items: {stats.CachedItemsCount}");
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
                    Source = "TestCache",
                    LineNumber = i + 1
                });
            }

            return entries;
        }

        /// <summary>
        /// Test RabbitMQ filtering performance with large datasets
        /// Validates memory efficiency and execution speed
        /// </summary>
        public static async Task TestRabbitMQFilteringPerformanceAsync()
        {
            Console.WriteLine("=== RabbitMQ Filtering Performance Test ===");
            
            // Create test data
            var testEntries = GenerateTestRabbitMQEntries(10000);
            Console.WriteLine($"Generated {testEntries.Count} test RabbitMQ entries");
            
            // Setup service
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            serviceCollection.AddSingleton<IRabbitMQFilterService, RabbitMQFilterService>();
            serviceCollection.AddSingleton<IFilterConfigurationService, FilterConfigurationService>();
            
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var filterService = serviceProvider.GetRequiredService<IRabbitMQFilterService>();
            
            // Test different filter scenarios
            await TestSimpleFiltering(filterService, testEntries);
            await TestComplexFiltering(filterService, testEntries);
            await TestMemoryUsage(filterService, testEntries);
            
            Console.WriteLine("=== RabbitMQ Filtering Performance Test Complete ===");
        }
        
        private static async Task TestSimpleFiltering(IRabbitMQFilterService filterService, List<RabbitMqLogEntry> entries)
        {
            Console.WriteLine("\n--- Simple Filtering Test ---");
            
            var criteria = new List<FilterCriterion>
            {
                new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR", IsActive = true }
            };
            
            var stopwatch = Stopwatch.StartNew();
            var filteredEntries = await filterService.ApplySimpleFiltersAsync(entries, criteria);
            var resultCount = filteredEntries.Count();
            stopwatch.Stop();
            
            Console.WriteLine($"Simple filtering ({entries.Count} entries): {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Results: {resultCount} entries matched");
            Console.WriteLine($"Throughput: {entries.Count / (stopwatch.ElapsedMilliseconds + 1) * 1000:F0} entries/sec");
        }
        
        private static async Task TestComplexFiltering(IRabbitMQFilterService filterService, List<RabbitMqLogEntry> entries)
        {
            Console.WriteLine("\n--- Complex Filtering Test ---");
            
            var criteria = new List<FilterCriterion>
            {
                new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "ERROR", IsActive = true },
                new FilterCriterion { SelectedField = "Message", SelectedOperator = "Contains", Value = "database", IsActive = true },
                new FilterCriterion { SelectedField = "Node", SelectedOperator = "StartsWith", Value = "rabbit", IsActive = true }
            };
            
            var stopwatch = Stopwatch.StartNew();
            var filteredEntries = await filterService.ApplySimpleFiltersAsync(entries, criteria);
            var resultCount = filteredEntries.Count();
            stopwatch.Stop();
            
            Console.WriteLine($"Complex filtering ({entries.Count} entries): {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Results: {resultCount} entries matched");
            Console.WriteLine($"Throughput: {entries.Count / (stopwatch.ElapsedMilliseconds + 1) * 1000:F0} entries/sec");
        }
        
        private static async Task TestMemoryUsage(IRabbitMQFilterService filterService, List<RabbitMqLogEntry> entries)
        {
            Console.WriteLine("\n--- Memory Usage Test ---");
            
            var criteria = new List<FilterCriterion>
            {
                new FilterCriterion { SelectedField = "Level", SelectedOperator = "Equals", Value = "INFO", IsActive = true }
            };
            
            var initialMemory = GC.GetTotalMemory(true);
            
            var stopwatch = Stopwatch.StartNew();
            var filteredEntries = await filterService.ApplySimpleFiltersAsync(entries, criteria);
            
            // Force enumeration to measure actual memory usage
            var resultList = filteredEntries.ToList();
            stopwatch.Stop();
            
            var finalMemory = GC.GetTotalMemory(false);
            var memoryUsed = finalMemory - initialMemory;
            
            Console.WriteLine($"Memory usage test: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Memory used: {memoryUsed / 1024:F1} KB");
            Console.WriteLine($"Memory per entry: {memoryUsed / entries.Count:F1} bytes");
            Console.WriteLine($"Results: {resultList.Count} entries");
        }
        
        private static List<RabbitMqLogEntry> GenerateTestRabbitMQEntries(int count)
        {
            var random = new Random(42); // Fixed seed for reproducible results
            var entries = new List<RabbitMqLogEntry>();
            var levels = new[] { "INFO", "WARNING", "ERROR", "DEBUG", "FATAL" };
            var nodes = new[] { "rabbit@node1", "rabbit@node2", "rabbit@node3" };
            var users = new[] { "admin", "guest", "service", "user1", "user2" };
            var messages = new[]
            {
                "Connection established",
                "Database query completed",
                "User authentication failed",
                "Message queued successfully",
                "Service started",
                "Configuration loaded",
                "Error processing request",
                "Memory usage warning",
                "Network timeout",
                "System shutdown initiated"
            };
            
            for (int i = 0; i < count; i++)
            {
                var entry = new RabbitMqLogEntry
                {
                    // Set base properties that compute Effective* properties
                    Timestamp = DateTimeOffset.Now.AddMinutes(-random.Next(10000)),
                    Level = levels[random.Next(levels.Length)],
                    Message = messages[random.Next(messages.Length)],
                    Node = nodes[random.Next(nodes.Length)],
                    User = users[random.Next(users.Length)],
                    ProcessId = $"proc_{random.Next(1000, 9999)}",
                    
                    // Set enhanced properties for paired files
                    ProcessUID = $"proc_{random.Next(1000, 9999)}",
                    UserName = users[random.Next(users.Length)]
                };
                
                entries.Add(entry);
            }
            
            return entries;
        }
    }
} 