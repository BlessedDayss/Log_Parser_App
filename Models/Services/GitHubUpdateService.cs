using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
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
                
                // Log the versions being compared
                Debug.WriteLine($"Comparing versions - Current: {currentVersion}, Latest: {latestVersionString}");
                
                // Only set IsUpdateAvailable to true if the latest version is greater than current
                var isUpdateAvailable = CompareVersions(latestVersionString, currentVersion) > 0;
                
                // If versions are equal or current is higher, don't suggest update
                if (!isUpdateAvailable)
                {
                    Debug.WriteLine("No update needed: current version is equal or higher than latest");
                }
                
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
        
        private static string GetAssetDownloadUrl(GitHubRelease release)
        {
            if (release.Assets.Length == 0)
            {
                return release.ZipballUrl;
            }
            
            // First look specifically for .exe files (prioritize them)
            foreach (var asset in release.Assets)
            {
                if (asset.Name.EndsWith(".exe"))
                {
                    Debug.WriteLine($"Found executable for update: {asset.Name}");
                    return asset.BrowserDownloadUrl;
                }
            }
            
            // If no .exe found, look for .zip files
            foreach (var asset in release.Assets)
            {
                if (asset.Name.EndsWith(".zip"))
                {
                    Debug.WriteLine($"No executable found, using zip file: {asset.Name}");
                    return asset.BrowserDownloadUrl;
                }
            }
            
            return release.ZipballUrl;
        }
        
        private static int CompareVersions(string version1, string? version2)
        {
            if (string.IsNullOrEmpty(version2))
            {
                return 1; 
            }
            
            try
            {
                // Normalize versions to ensure 4 components (major.minor.build.revision)
                string normalizedV1 = NormalizeVersionString(version1);
                string normalizedV2 = NormalizeVersionString(version2);
                
                Debug.WriteLine($"Normalized versions - Current: {normalizedV2}, Latest: {normalizedV1}");
                
                var v1 = new Version(normalizedV1);
                var v2 = new Version(normalizedV2);
                
                int result = v1.CompareTo(v2);
                Debug.WriteLine($"Version comparison result: {result} (>0 means update available)");
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing versions: {version1} or {version2}. Exception: {ex.Message}");
                return 0; // Don't suggest update if we can't parse versions
            }
        }
        
        // Helper method to normalize version strings to ensure 4 components
        private static string NormalizeVersionString(string version)
        {
            // Remove any 'v' prefix if present
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                version = version.Substring(1);
            }
            
            // Split by dots
            var parts = version.Split('.');
            
            // Ensure we have exactly 4 components (major.minor.build.revision)
            var result = new int[4];
            for (int i = 0; i < Math.Min(parts.Length, 4); i++)
            {
                if (int.TryParse(parts[i], out int value))
                {
                    result[i] = value;
                }
            }
            
            return $"{result[0]}.{result[1]}.{result[2]}.{result[3]}";
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