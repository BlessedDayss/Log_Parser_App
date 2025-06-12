using System;
using System.Text.Json.Serialization;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Represents a RabbitMQ log entry parsed from JSON format.
    /// Supports both simple RabbitMQ logs and MassTransit message structures.
    /// </summary>
    public class RabbitMqLogEntry
    {
        // Simple RabbitMQ log fields (for direct JSON logs)
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

        // MassTransit message structure support
        [JsonPropertyName("headers")]
        public RabbitMqHeaders? Headers { get; set; }

        [JsonPropertyName("properties")]
        public RabbitMqProperties? Properties { get; set; }

        // Raw JSON for debugging and additional data preservation
        public string RawJson { get; set; } = string.Empty;

        // Computed properties that work for both structures
        public DateTimeOffset? EffectiveTimestamp => 
            Timestamp ?? Headers?.FaultTimestamp ?? DateTimeOffset.Now;

        public string? EffectiveLevel => 
            Level ?? (Headers?.FaultExceptionType != null ? "error" : "info");

        public string? EffectiveMessage => 
            Message ?? Headers?.FaultMessage ?? "MassTransit message";

        public string? EffectiveNode => 
            Node ?? Headers?.HostMachineName;

        public string? EffectiveNodeDisplay => 
            !string.IsNullOrEmpty(EffectiveNode) ? EffectiveNode : "Unknown Host";

        public string? EffectiveQueue => 
            Queue ?? Properties?.Exchange;

        public string? EffectiveProcessId => 
            ProcessId ?? Headers?.HostProcessId;

        public string? EffectiveStackTrace => 
            Headers?.FaultStackTrace;

        public string? EffectiveConsumerType => 
            Headers?.FaultConsumerType;

        /// <summary>
        /// Converts RabbitMqLogEntry to LogEntry for compatibility with existing system
        /// </summary>
        /// <returns>LogEntry with mapped RabbitMQ data</returns>
        public LogEntry ToLogEntry()
        {
            return new LogEntry
            {
                Timestamp = (EffectiveTimestamp ?? DateTimeOffset.Now).DateTime,
                Level = EffectiveLevel ?? "INFO",
                Message = EffectiveMessage ?? string.Empty,
                Source = EffectiveNode ?? "RabbitMQ",
                RawData = RawJson,
                CorrelationId = Properties?.MessageId ?? Connection
            };
        }

        /// <summary>
        /// Creates a display-friendly string representation
        /// </summary>
        /// <returns>Formatted string for UI display</returns>
        public override string ToString()
        {
            var timestamp = EffectiveTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
            var level = EffectiveLevel ?? "INFO";
            var message = EffectiveMessage ?? "No message";
            var node = EffectiveNode ?? "Unknown";
            
            return $"[{timestamp}] {level} - {node}: {message}";
        }
    }

    /// <summary>
    /// Represents MassTransit headers structure
    /// </summary>
    public class RabbitMqHeaders
    {
        [JsonPropertyName("Content-Type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("ContextType")]
        public string? ContextType { get; set; }

        [JsonPropertyName("MT-Fault-ConsumerType")]
        public string? FaultConsumerType { get; set; }

        [JsonPropertyName("MT-Fault-ExceptionType")]
        public string? FaultExceptionType { get; set; }

        [JsonPropertyName("MT-Fault-Message")]
        public string? FaultMessage { get; set; }

        [JsonPropertyName("MT-Fault-MessageType")]
        public string? FaultMessageType { get; set; }

        [JsonPropertyName("MT-Fault-StackTrace")]
        public string? FaultStackTrace { get; set; }

        [JsonPropertyName("MT-Fault-Timestamp")]
        public DateTimeOffset? FaultTimestamp { get; set; }

        [JsonPropertyName("MT-Host-Assembly")]
        public string? HostAssembly { get; set; }

        [JsonPropertyName("MT-Host-AssemblyVersion")]
        public string? HostAssemblyVersion { get; set; }

        [JsonPropertyName("MT-Host-FrameworkVersion")]
        public string? HostFrameworkVersion { get; set; }

        [JsonPropertyName("MT-Host-MachineName")]
        public string? HostMachineName { get; set; }

        [JsonPropertyName("MT-Host-MassTransitVersion")]
        public string? HostMassTransitVersion { get; set; }

        [JsonPropertyName("MT-Host-OperatingSystemVersion")]
        public string? HostOperatingSystemVersion { get; set; }

        [JsonPropertyName("MT-Host-ProcessId")]
        public string? HostProcessId { get; set; }

        [JsonPropertyName("MT-Host-ProcessName")]
        public string? HostProcessName { get; set; }

        [JsonPropertyName("MT-Reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("publishId")]
        public string? PublishId { get; set; }
    }

    /// <summary>
    /// Represents MassTransit properties structure
    /// </summary>
    public class RabbitMqProperties
    {
        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }

        [JsonPropertyName("delivery_mode")]
        public int? DeliveryMode { get; set; }

        [JsonPropertyName("exchange")]
        public string? Exchange { get; set; }

        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }

        [JsonPropertyName("priority")]
        public int? Priority { get; set; }
    }
} 