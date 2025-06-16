using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Object pool interface for LogEntry instances
    /// Reduces memory allocations and garbage collection pressure
    /// </summary>
    public interface ILogEntryPool
    {
        /// <summary>
        /// Get a LogEntry instance from the pool
        /// Creates new instance if pool is empty
        /// </summary>
        /// <returns>LogEntry instance ready for use</returns>
        LogEntry Get();

        /// <summary>
        /// Return a LogEntry instance to the pool for reuse
        /// Instance will be reset before being returned to pool
        /// </summary>
        /// <param name="entry">LogEntry to return to pool</param>
        void Return(LogEntry entry);

        /// <summary>
        /// Get current pool statistics
        /// </summary>
        /// <returns>Pool performance statistics</returns>
        PoolStatistics GetStatistics();

        /// <summary>
        /// Clear all pooled instances
        /// </summary>
        void Clear();

        /// <summary>
        /// Get current number of available instances in pool
        /// </summary>
        int AvailableCount { get; }

        /// <summary>
        /// Get maximum pool capacity
        /// </summary>
        int MaxCapacity { get; }
    }
}
