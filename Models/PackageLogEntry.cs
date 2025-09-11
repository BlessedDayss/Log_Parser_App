namespace Log_Parser_App.Models
{

	#region Class: PackageLogEntry

	public class PackageLogEntry : LogEntry
	{

		#region Properties: Public

		public string PackageId { get; set; } = string.Empty;

		public string Version { get; set; } = string.Empty;

		public string Status { get; set; } = string.Empty;

		public string Dependencies { get; set; } = string.Empty;

		public string Operation { get; set; } = string.Empty;

		public string OperationIcon => Operation.ToLowerInvariant() switch {
			"install" => "📥",
			"update" => "🔄",
			"remove" => "🗑️",
			"rollback" => "↩️",
			_ => "📦"
		};

		#endregion

	}

	#endregion

}
