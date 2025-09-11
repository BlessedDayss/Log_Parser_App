namespace Log_Parser_App.Models
{
	using System;
	using System.Text.Json.Serialization;

	#region Class: RabbitMqLogEntry

	/// <summary>
	/// Represents a RabbitMQ log entry parsed from JSON format.
	/// Supports both simple RabbitMQ logs and MassTransit message structures.
	/// Enhanced with paired file support for processUId and UserName extraction.
	/// </summary>
	public class RabbitMqLogEntry
	{
		// Simple RabbitMQ log fields (for direct JSON logs)

		#region Properties: Public

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

		// Enhanced properties for paired file support

		/// <summary>
		/// Process UID extracted from message.processUId in paired files
		/// </summary>
		public string? ProcessUID { get; set; }

		/// <summary>
		/// Username extracted from headers.Context.userContext.UserName in paired files
		/// </summary>
		public string? UserName { get; set; }

		/// <summary>
		/// Sent time extracted from sentTime field in paired files
		/// </summary>
		public DateTimeOffset? SentTime { get; set; }

		/// <summary>
		/// Stack trace extracted from paired headers file
		/// </summary>
		public string? StackTrace { get; set; }

		/// <summary>
		/// Error message extracted from MT-Fault-Message in headers
		/// </summary>
		public string? FaultMessage { get; set; }

		/// <summary>
		/// Error timestamp extracted from MT-Fault-Timestamp in headers
		/// </summary>
		public DateTimeOffset? FaultTimestamp { get; set; }

		// MassTransit message structure support

		[JsonPropertyName("headers")]
		public RabbitMqHeaders? Headers { get; set; }

		[JsonPropertyName("properties")]
		public RabbitMqProperties? Properties { get; set; }

		// Raw JSON for debugging and additional data preservation

		public string RawJson { get; set; } = string.Empty;

		// Computed properties that work for both structures

		public DateTimeOffset? EffectiveTimestamp =>
			SentTime ?? FaultTimestamp ?? Timestamp ?? Headers?.FaultTimestamp ?? DateTimeOffset.Now;

		public string? EffectiveLevel =>
			Level ?? (Headers?.FaultExceptionType != null || !string.IsNullOrEmpty(FaultMessage) ? "error" : "info");

		public string? EffectiveMessage =>
			FaultMessage ?? Message ?? Headers?.FaultMessage ?? "MassTransit message";

		public string? EffectiveNode =>
			Node ?? Headers?.HostMachineName;

		public string? EffectiveNodeDisplay =>
			!string.IsNullOrEmpty(EffectiveNode) ? EffectiveNode : "Unknown Host";

		public string? EffectiveQueue =>
			Queue ?? Properties?.Exchange;

		public string? EffectiveProcessId =>
			ProcessId ?? Headers?.HostProcessId;

		public string? EffectiveStackTrace =>
			StackTrace ?? Headers?.FaultStackTrace;

		public string? EffectiveConsumerType =>
			Headers?.FaultConsumerType;

		/// <summary>
		/// Effective ProcessUID - returns ProcessUID if available, otherwise falls back to ProcessId
		/// </summary>
		public string? EffectiveProcessUID =>
			ProcessUID ?? EffectiveProcessId;

		/// <summary>
		/// Effective UserName - returns UserName if available, otherwise falls back to User
		/// </summary>
		public string? EffectiveUserName =>
			UserName ?? User;

		#endregion

		#region Methods: Public

		/// <summary>
		/// Creates a simplified RabbitMQ log entry with only essential fields
		/// </summary>
		/// <param name="processUID">Process UID from file</param>
		/// <param name="userName">Username from file</param>
		/// <param name="sentTime">Sent time from file</param>
		/// <param name="faultMessage">Error message from headers</param>
		/// <param name="stackTrace">Stack trace from headers</param>
		/// <param name="faultTimestamp">Error timestamp from headers</param>
		/// <returns>Simplified RabbitMqLogEntry</returns>
		public static RabbitMqLogEntry CreateSimplified(
			string? processUID = null,
			string? userName = null,
			DateTimeOffset? sentTime = null,
			string? faultMessage = null,
			string? stackTrace = null,
			DateTimeOffset? faultTimestamp = null)
		{
			// Debug output to console
			System.Console.WriteLine($"[DEBUG] CreateSimplified called:");
			System.Console.WriteLine($"  ProcessUID: {processUID}");
			System.Console.WriteLine($"  UserName: {userName}");
			System.Console.WriteLine($"  SentTime: {sentTime}");
			System.Console.WriteLine($"  FaultMessage: {faultMessage?.Substring(0, Math.Min(100, faultMessage?.Length ?? 0))}");
			System.Console.WriteLine($"  StackTrace length: {stackTrace?.Length ?? 0}");
			
			return new RabbitMqLogEntry
			{
				ProcessUID = processUID,
				UserName = userName,
				SentTime = sentTime,
				FaultMessage = faultMessage,
				StackTrace = stackTrace,
				FaultTimestamp = faultTimestamp,
				Level = !string.IsNullOrEmpty(faultMessage) ? "error" : "info"
			};
		}

		/// <summary>
		/// Converts RabbitMqLogEntry to LogEntry for compatibility with existing system
		/// </summary>
		/// <returns>LogEntry with mapped RabbitMQ data</returns>
		public LogEntry ToLogEntry() {
			return new LogEntry {
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
		public override string ToString() {
			string timestamp = EffectiveTimestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
			string level = EffectiveLevel ?? "INFO";
			string message = EffectiveMessage ?? "No message";
			string node = EffectiveNode ?? "Unknown";
			return $"[{timestamp}] {level} - {node}: {message}";
		}

		#endregion

	}

	#endregion

	#region Class: RabbitMqHeaders

	/// <summary>
	/// Represents MassTransit headers structure
	/// </summary>
	public class RabbitMqHeaders
	{

		#region Properties: Public

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

		#endregion

	}

	#endregion

	#region Class: RabbitMqProperties

	/// <summary>
	/// Represents MassTransit properties structure
	/// </summary>
	public class RabbitMqProperties
	{

		#region Properties: Public

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

		#endregion

	}

	#endregion

}
