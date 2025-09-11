using System;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Enumeration of logical operators for filter composition.
    /// </summary>
    public enum LogicalOperator
    {
        And,
        Or,
        Not
    }
    
    /// <summary>
    /// Enumeration of comparison operators for filter criteria.
    /// </summary>
    public enum ComparisonOperator
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Between,
        Regex
    }
    
    /// <summary>
    /// Context information for filter evaluation.
    /// Carries state and configuration during filter processing.
    /// </summary>
    public class FilterContext
    {
        /// <summary>
        /// Estimated total count of items being filtered.
        /// Used for selectivity calculations and optimization.
        /// </summary>
        public int EstimatedTotalCount { get; set; }
        
        /// <summary>
        /// Maximum execution time allowed for filtering.
        /// Used for performance monitoring and timeouts.
        /// </summary>
        public TimeSpan MaxExecutionTime { get; set; } = TimeSpan.FromSeconds(30);
        
        /// <summary>
        /// Whether to enable query optimization.
        /// Controls execution order optimization based on selectivity.
        /// </summary>
        public bool EnableOptimization { get; set; } = true;
        
        /// <summary>
        /// Whether to enable parallel execution for independent filters.
        /// Controls parallelization of OR operations and independent filter branches.
        /// </summary>
        public bool EnableParallelExecution { get; set; } = true;
        
        /// <summary>
        /// Batch size for streaming operations.
        /// Controls memory usage vs performance trade-off.
        /// </summary>
        public int BatchSize { get; set; } = 1000;
    }
    
    /// <summary>
    /// Information about filter execution for debugging and monitoring.
    /// </summary>
    public class FilterExecutionInfo
    {
        /// <summary>
        /// Time taken to execute the filter.
        /// </summary>
        public TimeSpan ExecutionTime { get; set; }
        
        /// <summary>
        /// Number of items processed.
        /// </summary>
        public int ItemsProcessed { get; set; }
        
        /// <summary>
        /// Number of items that passed the filter.
        /// </summary>
        public int ItemsMatched { get; set; }
        
        /// <summary>
        /// Actual selectivity ratio (matched/processed).
        /// </summary>
        public double ActualSelectivity => ItemsProcessed > 0 ? (double)ItemsMatched / ItemsProcessed : 0.0;
        
        /// <summary>
        /// Description of the filter that was executed.
        /// </summary>
        public string FilterDescription { get; set; } = string.Empty;
    }
} 