using System;

namespace Log_Parser_App.Models
{

	#region Class: IisLogEntry

	public class IisLogEntry
	{ 

		#region Properties: Public

		public DateTimeOffset? DateTime { get; set; }

		public string? ClientIPAddress { get; set; }

		public string? UserName { get; set; }

		public string? ServiceName { get; set; }

		public string? ServerName { get; set; }

		public string? ServerIPAddress { get; set; }

		public int? ServerPort { get; set; }

		public string? Method { get; set; }

		public string? UriStem { get; set; }

		public string? UriQuery { get; set; }

		public int? HttpStatus { get; set; }

		public int? Win32Status { get; set; }

		public long? BytesSent { get; set; }

		public long? BytesReceived { get; set; }

		public int? TimeTaken { get; set; }

		public string? ProtocolVersion { get; set; }

		public string? Host { get; set; }

		public string? UserAgent { get; set; }

		public string? Cookie { get; set; }

		public string? RawLine { get; set; }

		public string? ShortUserAgent 
		{
			get
			{
				if (string.IsNullOrEmpty(UserAgent))
					return "Not Specified";
				return UserAgent.Length <= MaxUserAgentDisplayLength
					? UserAgent
					: string.Concat(UserAgent.AsSpan(0, MaxUserAgentDisplayLength), "...");
			}
		}

		#endregion

		#region Constants: Private

		private const int MaxUserAgentDisplayLength = 50;

		#endregion

	}

	#endregion

}