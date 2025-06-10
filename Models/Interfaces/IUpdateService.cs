namespace Log_Parser_App.Models.Interfaces
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class UpdateInfo
    {
        public Version? Version { get; set; }
        public string? ReleaseName { get; set; }
        public string? ReleaseNotes { get; set; }
        public string? DownloadUrl { get; set; }
        public string? TagName { get; set; }
        public DateTime? PublishedAt { get; set; }
        public bool RequiresRestart { get; set; }
        public List<string> ChangeLog { get; set; } = new List<string>();
    }

    public interface IUpdateService
    {
        Version GetCurrentVersion();
        Task<UpdateInfo?> CheckForUpdatesAsync();
        Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null);
        Task<bool> InstallUpdateAsync(string updateFilePath);
    }
}