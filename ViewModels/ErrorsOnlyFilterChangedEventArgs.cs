using System;
using Log_Parser_App.Models;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// Event arguments for when the errors-only filter is changed
    /// </summary>
    public class ErrorsOnlyFilterChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether errors-only filter is enabled
        /// </summary>
        public bool IsErrorsOnlyEnabled { get; }

        /// <summary>
        /// The log format type of the tab that triggered the change
        /// </summary>
        public LogFormatType LogType { get; }

        /// <summary>
        /// Timestamp when the change occurred
        /// </summary>
        public DateTime Timestamp { get; }

        public ErrorsOnlyFilterChangedEventArgs(bool isErrorsOnlyEnabled, LogFormatType logType)
        {
            IsErrorsOnlyEnabled = isErrorsOnlyEnabled;
            LogType = logType;
            Timestamp = DateTime.UtcNow;
        }
    }
} 