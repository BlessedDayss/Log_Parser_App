namespace Log_Parser_App.Interfaces
{
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

	#region Class: UpdateInfo

	public class UpdateInfo
	{

		#region Properties: Public

		public Version? Version { get; set; }

		public string? ReleaseName { get; set; }

		public string? ReleaseNotes { get; set; }

		public string? DownloadUrl { get; set; }

		public string? TagName { get; set; }

		public DateTime? PublishedAt { get; set; }

		public bool RequiresRestart { get; set; }

		public List<string> ChangeLog { get; set; } = new List<string>();

		#endregion

	}

	#endregion

	#region Interface: IUpdateService

	public interface IUpdateService
	{

		#region Methods: Public

		Version GetCurrentVersion();

		Task<UpdateInfo?> CheckForUpdatesAsync();

		Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null);

		Task<bool> InstallUpdateAsync(string updateFilePath);

		#endregion

	}

	#endregion

}
