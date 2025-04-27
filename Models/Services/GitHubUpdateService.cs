using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using Log_Parser_App.Models.Interfaces;

namespace Log_Parser_App.Models.Services
{
    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;
        
        public GitHubUpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Log-Parser-App");
            _owner = "BlessedDayss"; // Replace with your GitHub username
            _repo = "Log_Parser_App";  // Replace with your repository name
        }
        
        public string? GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }
        
        public async Task<Interfaces.UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var releaseUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(releaseUrl);
                
                if (release == null)
                {
                    return new Interfaces.UpdateInfo { IsUpdateAvailable = false };
                }
                
                var latestVersionString = release.TagName.StartsWith("v") 
                    ? release.TagName.Substring(1) 
                    : release.TagName;
                
                var currentVersion = GetCurrentVersion();
                var isUpdateAvailable = CompareVersions(latestVersionString, currentVersion) > 0;
                
                return new Interfaces.UpdateInfo
                {
                    IsUpdateAvailable = isUpdateAvailable,
                    LatestVersion = latestVersionString,
                    ReleaseNotes = release.Body,
                    DownloadUrl = GetAssetDownloadUrl(release),
                    ReleaseDate = release.PublishedAt
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return new Interfaces.UpdateInfo { IsUpdateAvailable = false };
            }
        }
        
        public async Task<bool> UpdateApplicationAsync(string downloadUrl)
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "LogParserUpdate");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                Directory.CreateDirectory(tempDir);
                
                var updateFilePath = Path.Combine(tempDir, "update.zip");
                await DownloadFileAsync(downloadUrl, updateFilePath);
                
                // Extract and replace the application files
                // This would typically involve:
                // 1. Extracting the update package
                // 2. Closing the current application
                // 3. Replacing the application files
                // 4. Restarting the application
                
                // For a simple implementation, you might use a separate updater application
                // or create a batch script to handle the update process
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating application: {ex.Message}");
                return false;
            }
        }
        
        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }
        
        private string GetAssetDownloadUrl(GitHubRelease release)
        {
            if (release.Assets.Length == 0)
            {
                return release.ZipballUrl;
            }
            
            foreach (var asset in release.Assets)
            {
                if (asset.Name.EndsWith(".zip") || asset.Name.EndsWith(".exe"))
                {
                    return asset.BrowserDownloadUrl;
                }
            }
            
            return release.ZipballUrl;
        }
        
        private int CompareVersions(string version1, string? version2)
        {
            if (string.IsNullOrEmpty(version2))
            {
                return 1; // новая версия доступна, если текущая версия - null или пустая
            }
            
            try
            {
                var v1 = new Version(version1);
                var v2 = new Version(version2);
                return v1.CompareTo(v2);
            }
            catch
            {
                // в случае ошибки при разборе версии, считаем что новая версия доступна
                Debug.WriteLine($"Error parsing versions: {version1} or {version2}");
                return 1;
            }
        }
    }
    
    public class GitHubRelease(
        string tagName,
        string name,
        string body,
        string publishedAt,
        string zipballUrl,
        GitHubAsset[] assets)
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; init; } = tagName;

        [JsonPropertyName("name")]
        public string Name { get; init; } = name;

        [JsonPropertyName("body")]
        public string Body { get; init; } = body;

        [JsonPropertyName("published_at")]
        public string PublishedAt { get; init; } = publishedAt;

        [JsonPropertyName("zipball_url")]
        public string ZipballUrl { get; init; } = zipballUrl;

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; init; } = assets;
    }
    
    public abstract class GitHubAsset(string name, string browserDownloadUrl)
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = name;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = browserDownloadUrl;
    }
} 