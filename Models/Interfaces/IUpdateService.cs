namespace Log_Parser_App.Models.Interfaces
{
    using System.Threading.Tasks;


    public interface IUpdateService
    {
        string? GetCurrentVersion();
        Task<UpdateInfo> CheckForUpdatesAsync();
        Task<bool> UpdateApplicationAsync(string downloadUrl);
    }

    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public string LatestVersion { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
    }
}