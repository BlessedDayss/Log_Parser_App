using System;
using System.Collections.Concurrent;
using System.Threading;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Object pool implementation for LogEntry instances
    /// Reduces memory allocations and garbage collection pressure
    /// </summary>
    public class LogEntryPool : ILogEntryPool, IDisposable
    {
        private readonly ILogger<LogEntryPool> _logger;
        private readonly ConcurrentQueue<LogEntry> _pool;
        private readonly int _maxCapacity;
        private volatile int _currentCount;
        private readonly PoolStatistics _statistics;
        private readonly object _statsLock = new object();
        private bool _disposed;

        public LogEntryPool(ILogger<LogEntryPool> logger, int maxCapacity = 1000)
        {
            _logger = logger;
            _maxCapacity = Math.Max(maxCapacity, 10);
            _pool = new ConcurrentQueue<LogEntry>();
            _currentCount = 0;
            _statistics = new PoolStatistics
            {
                MaxPoolSize = _maxCapacity
            };

            _logger.LogDebug("LogEntryPool initialized with max capacity: {MaxCapacity}", _maxCapacity);
        }

        public int AvailableCount => _currentCount;
        public int MaxCapacity => _maxCapacity;

        /// <summary>
        /// Get a LogEntry instance from the pool
        /// Creates new instance if pool is empty
        /// </summary>
        public LogEntry Get()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LogEntryPool));

            lock (_statsLock)
            {
                _statistics.TotalGets++;
            }

            if (_pool.TryDequeue(out var entry))
            {
                Interlocked.Decrement(ref _currentCount);
                
                lock (_statsLock)
                {
                    _statistics.PoolHits++;
                    _statistics.CurrentPoolSize = _currentCount;
                }

                _logger.LogTrace("Retrieved LogEntry from pool. Available: {Available}", _currentCount);
                return entry;
            }

            // Pool is empty, create new instance
            var newEntry = new LogEntry();
            
            lock (_statsLock)
            {
                _statistics.PoolMisses++;
                _statistics.TotalInstancesCreated++;
                
                // Estimate memory saved (approximate LogEntry size: 200 bytes)
                _statistics.MemorySavedBytes = _statistics.PoolHits * 200;
            }

            _logger.LogTrace("Created new LogEntry instance. Pool misses: {Misses}", _statistics.PoolMisses);
            return newEntry;
        }

        /// <summary>
        /// Return a LogEntry instance to the pool for reuse
        /// Instance will be reset before being returned to pool
        /// </summary>
        public void Return(LogEntry entry)
        {
            if (_disposed)
                return;

            if (entry == null)
            {
                _logger.LogWarning("Attempted to return null LogEntry to pool");
                return;
            }

            // Reset the entry to clean state
            ResetLogEntry(entry);

            lock (_statsLock)
            {
                _statistics.TotalReturns++;
            }

            // Only add to pool if we haven't exceeded capacity
            if (_currentCount < _maxCapacity)
            {
                _pool.Enqueue(entry);
                var newCount = Interlocked.Increment(ref _currentCount);
                
                lock (_statsLock)
                {
                    _statistics.CurrentPoolSize = newCount;
                }

                _logger.LogTrace("Returned LogEntry to pool. Available: {Available}", newCount);
            }
            else
            {
                // Pool is full, let GC handle this instance
                _logger.LogTrace("Pool at capacity, discarding LogEntry instance");
            }
        }

        /// <summary>
        /// Get current pool statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            lock (_statsLock)
            {
                return new PoolStatistics
                {
                    TotalGets = _statistics.TotalGets,
                    TotalReturns = _statistics.TotalReturns,
                    PoolHits = _statistics.PoolHits,
                    PoolMisses = _statistics.PoolMisses,
                    CurrentPoolSize = _statistics.CurrentPoolSize,
                    MaxPoolSize = _statistics.MaxPoolSize,
                    TotalInstancesCreated = _statistics.TotalInstancesCreated,
                    MemorySavedBytes = _statistics.MemorySavedBytes
                };
            }
        }

        /// <summary>
        /// Clear all pooled instances
        /// </summary>
        public void Clear()
        {
            if (_disposed)
                return;

            var clearedCount = 0;
            while (_pool.TryDequeue(out _))
            {
                clearedCount++;
            }

            Interlocked.Exchange(ref _currentCount, 0);

            lock (_statsLock)
            {
                _statistics.CurrentPoolSize = 0;
            }

            _logger.LogDebug("Cleared {Count} instances from pool", clearedCount);
        }

        private void ResetLogEntry(LogEntry entry)
        {
            // Reset core properties that exist in LogEntry
            entry.Timestamp = DateTime.Now;
            entry.Level = "INFO";
            entry.Source = string.Empty;
            entry.Message = string.Empty;
            entry.CorrelationId = null;
            entry.ErrorType = null;
            entry.ErrorDescription = null;
            entry.ErrorRecommendations?.Clear();
            entry.FilePath = null;
            entry.LineNumber = null;
            entry.SourceTabTitle = null;
            entry.Recommendation = null;
            entry.OpenFileCommand = null;
            entry.StackTrace = null;
            entry.Logger = null;
            entry.IsExpanded = false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logger.LogDebug("Disposing LogEntryPool with {Count} instances", _currentCount);

            Clear();
            _disposed = true;

            // Log final statistics
            var finalStats = GetStatistics();
            _logger.LogInformation(
                "LogEntryPool disposed. Final stats - Gets: {Gets}, Returns: {Returns}, Hits: {Hits}, Misses: {Misses}, Hit Ratio: {HitRatio:P2}, Memory Saved: {MemorySaved} bytes",
                finalStats.TotalGets, finalStats.TotalReturns, finalStats.PoolHits, finalStats.PoolMisses, 
                finalStats.HitRatio, finalStats.MemorySavedBytes);
        }
    }
}
