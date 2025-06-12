using System;
using System.Text.Json.Serialization;

namespace Log_Parser_App.Models
{
    public class RabbitMqLogEntry
    {
        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; set; }

        [JsonPropertyName("level")]
        public string? Level { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("node")]
        public string? Node { get; set; }

        [JsonPropertyName("pid")]
        public string? ProcessId { get; set; }

        [JsonPropertyName("queue")]
        public string? Queue { get; set; }

        [JsonPropertyName("connection")]
        public string? Connection { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }

        [JsonPropertyName("vhost")]
        public string? VirtualHost { get; set; }

        [JsonIgnore]
        public string RawJson { get; set; } = string.Empty;

        public LogEntry ToLogEntry()
        {
            return new LogEntry
            {
                Timestamp = (Timestamp ?? DateTimeOffset.Now).DateTime,
                Level = Level ?? "INFO",
                Message = Message ?? string.Empty,
                Source = Node ?? "RabbitMQ",
                RawData = RawJson,
                CorrelationId = Connection
            };
        }
    }
} 