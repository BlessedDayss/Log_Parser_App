using System;

namespace Log_Parser_App.Models
{
    public class IISLogEntry
    {
        // Standard W3C Fields (nullable to accommodate customized IIS logs)
        public DateTimeOffset? DateTime { get; set; } // Combined Date and Time (Typically UTC)
        public string? ClientIPAddress { get; set; }   // c-ip
        public string? UserName { get; set; }          // cs-username
        public string? ServiceName { get; set; }       // s-sitename
        public string? ServerName { get; set; }        // s-computername
        public string? ServerIPAddress { get; set; }   // s-ip
        public int? ServerPort { get; set; }           // s-port
        public string? Method { get; set; }            // cs-method
        public string? UriStem { get; set; }           // cs-uri-stem
        public string? UriQuery { get; set; }          // cs-uri-query
        public int? HttpStatus { get; set; }          // sc-status
        public int? Win32Status { get; set; }         // sc-win32-status
        public long? BytesSent { get; set; }           // sc-bytes
        public long? BytesReceived { get; set; }       // cs-bytes
        public int? TimeTaken { get; set; }           // time-taken (milliseconds)
        public string? ProtocolVersion { get; set; }   // cs-version
        public string? Host { get; set; }              // cs-host
        public string? UserAgent { get; set; }         // cs(User-Agent)
        public string? Cookie { get; set; }            // cs(Cookie)

        // Raw line for reference or if parsing fails for specific fields
        public string? RawLine { get; set; }
    }
} 