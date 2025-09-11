using System;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Validation rule for log entries
    /// </summary>
    public class LogValidationRule
    {
        /// <summary>
        /// Name/identifier of the validation rule
        /// </summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable description of what this rule validates
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Function that performs the validation on a log entry
        /// Returns true if validation passes, false if it fails
        /// </summary>
        public Func<LogEntry, bool> ValidationFunc { get; set; } = _ => true;

        /// <summary>
        /// Severity level of validation failure
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Warning;

        /// <summary>
        /// Whether this rule is currently enabled
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Category of validation (e.g., "Format", "Content", "Performance")
        /// </summary>
        public string Category { get; set; } = "General";

        /// <summary>
        /// Expected value or pattern for validation (if applicable)
        /// </summary>
        public string? ExpectedValue { get; set; }

        /// <summary>
        /// Custom error message to display when validation fails
        /// </summary>
        public string? CustomErrorMessage { get; set; }
    }

    /// <summary>
    /// Severity levels for validation failures
    /// </summary>
    public enum ValidationSeverity
    {
        /// <summary>
        /// Informational - not a problem, just additional info
        /// </summary>
        Info,

        /// <summary>
        /// Warning - potential issue but not critical
        /// </summary>
        Warning,

        /// <summary>
        /// Error - significant issue that should be addressed
        /// </summary>
        Error,

        /// <summary>
        /// Critical - major issue that prevents proper processing
        /// </summary>
        Critical
    }
} 