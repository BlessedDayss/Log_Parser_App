using System;
using System.IO;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.Services.LevelDetection;

namespace Log_Parser_App.Services.Parsing
{
    /// <summary>
    /// Abstract base class for log line parsers using Template Method pattern
    /// SOLID: Single Responsibility - handles common parsing logic
    /// SOLID: Open/Closed - extensible through inheritance
    /// </summary>
    public abstract class AbstractLogLineParser : ILogLineParser
    {
        protected readonly LevelDetectionService _levelDetectionService;
        
        protected AbstractLogLineParser(LevelDetectionService levelDetectionService)
        {
            _levelDetectionService = levelDetectionService;
        }
        
        /// <summary>
        /// Template method that defines the parsing algorithm
        /// Concrete parsers implement specific format parsing logic
        /// </summary>
        public LogEntry? Parse(string line, int lineNumber, string filePath)
        {
            if (!IsLogLine(line))
                return null;
            
            try
            {
                // Step 1: Parse the format-specific parts (implemented by concrete parsers)
                var parsedData = ParseFormatSpecificData(line);
                
                if (parsedData == null)
                    return null;
                
                // Step 2: Detect level using centralized strategy service
                var detectedLevel = _levelDetectionService.DetectLevel(parsedData.Message, line);
                
                // Step 3: Create the log entry
                return new LogEntry
                {
                    Timestamp = parsedData.Timestamp,
                    Level = detectedLevel,
                    Message = parsedData.Message,
                    Source = string.IsNullOrWhiteSpace(parsedData.Source) ? Path.GetFileName(filePath) : parsedData.Source,
                    FilePath = filePath,
                    LineNumber = lineNumber,
                    RawData = line
                };
            }
            catch (Exception)
            {
                // If parsing fails, return null to let other parsers try
                return null;
            }
        }
        
        /// <summary>
        /// Checks if this parser can handle the given line format
        /// Must be implemented by concrete parsers
        /// </summary>
        public abstract bool IsLogLine(string line);
        
        /// <summary>
        /// Parses format-specific data from the log line
        /// Must be implemented by concrete parsers
        /// </summary>
        /// <param name="line">The log line to parse</param>
        /// <returns>Parsed data or null if parsing fails</returns>
        protected abstract ParsedLogData? ParseFormatSpecificData(string line);
        
        /// <summary>
        /// Data structure for parsed log information
        /// Used for communication between template method and concrete implementations
        /// </summary>
        protected class ParsedLogData
        {
            public DateTime Timestamp { get; set; }
            public string Message { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
        }
    }
} 