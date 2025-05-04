namespace Log_Parser_App.Models.Services
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Reflection;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using Log_Parser_App.Models.Interfaces;


    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;

        public GitHubUpdateService() {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Log-Parser-App");
            _owner = "BlessedDayss"; 
            _repo = "Log_Parser_App"; 
        }

        public string? GetCurrentVersion() {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString();
        }

        public async Task<Interfaces.UpdateInfo> CheckForUpdatesAsync() {
            try {
                string releaseUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(releaseUrl);

                if (release == null) {
                    return new Interfaces.UpdateInfo { IsUpdateAvailable = false };
                }
                string latestVersionString = release.TagName.StartsWith("v") ? release.TagName.Substring(1) : release.TagName;
                string? currentVersion = GetCurrentVersion();
                Debug.WriteLine($"Comparing versions - Current: {currentVersion}, Latest: {latestVersionString}");
                bool isUpdateAvailable = CompareVersions(latestVersionString, currentVersion) > 0;
                if (!isUpdateAvailable) {
                    Debug.WriteLine("No update needed: current version is equal or higher than latest");
                }

                return new Interfaces.UpdateInfo {
                    IsUpdateAvailable = isUpdateAvailable,
                    LatestVersion = latestVersionString,
                    ReleaseNotes = release.Body,
                    DownloadUrl = GetAssetDownloadUrl(release),
                    ReleaseDate = release.PublishedAt
                };
            } catch (Exception ex) {
                Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return new Interfaces.UpdateInfo { IsUpdateAvailable = false };
            }
        }

        public async Task<bool> UpdateApplicationAsync(string downloadUrl) {
            try {
                string tempDir = Path.Combine(Path.GetTempPath(), "LogParserUpdate");
                if (Directory.Exists(tempDir)) {
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
            } catch (Exception ex) {
                Debug.WriteLine($"Error updating application: {ex.Message}");
                return false;
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath) {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(fileStream);
        }

        private static string GetAssetDownloadUrl(GitHubRelease release) {
            if (release.Assets.Length == 0) {
                return release.ZipballUrl;
            }

            foreach (var asset in release.Assets) {
                if (!asset.Name.EndsWith(".exe"))
                    continue;
                Debug.WriteLine($"Found executable for update: {asset.Name}");
                return asset.BrowserDownloadUrl;
            }

            foreach (var asset in release.Assets) {
                if (!asset.Name.EndsWith(".zip"))
                    continue;
                Debug.WriteLine($"No executable found, using zip file: {asset.Name}");
                return asset.BrowserDownloadUrl;
            }

            return release.ZipballUrl;
        }

        private static int CompareVersions(string version1, string? version2) {
            if (string.IsNullOrEmpty(version2)) {
                return 1;
            }

            try {
                string normalizedV1 = NormalizeVersionString(version1);
                string normalizedV2 = NormalizeVersionString(version2);
                Debug.WriteLine($"Normalized versions - Current: {normalizedV2}, Latest: {normalizedV1}");
                var v1 = new Version(normalizedV1);
                var v2 = new Version(normalizedV2);
                int result = v1.CompareTo(v2);
                Debug.WriteLine($"Version comparison result: {result} (>0 means update available)");
                return result;
            } catch (Exception ex) {
                Debug.WriteLine($"Error parsing versions: {version1} or {version2}. Exception: {ex.Message}");
                return 0; 
            }
        }

        private static string NormalizeVersionString(string version) {
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) {
                version = version.Substring(1);
            }

            string[] parts = version.Split('.');

            int[] result = new int[4];
            for (int i = 0; i < Math.Min(parts.Length, 4); i++) {
                if (int.TryParse(parts[i], out int value)) {
                    result[i] = value;
                }
            }

            return $"{result[0]}.{result[1]}.{result[2]}.{result[3]}";
        }
    }

    public class GitHubRelease(string tagName, string name, string body, string publishedAt, string zipballUrl, GitHubAsset[] assets)
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