namespace Log_Parser_App.Services
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Text.Json;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Microsoft.Extensions.Logging;


    public class UpdateService(ILogger<UpdateService> logger) : IUpdateService
    {
        private const string UpdateCheckUrl = "https://api.github.com/repos/YourUsername/LogParserApp/releases/latest";

        public async Task<UpdateInfo?> CheckForUpdatesAsync() {
            try {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "LogParserApp");

                string response = await httpClient.GetStringAsync(UpdateCheckUrl);
                var githubRelease = JsonSerializer.Deserialize<GitHubReleaseResponse>(response);

                if (githubRelease == null) {
                    logger.LogWarning("No update information found");
                    return null;
                }

                var latestVersion = Version.Parse(githubRelease.TagName.TrimStart('v'));
                var currentVersion = GetCurrentVersion();

                if (latestVersion > currentVersion) {
                    return new UpdateInfo {
                        Version = latestVersion,
                        ReleaseName = githubRelease.Name,
                        ReleaseNotes = githubRelease.Body,
                        DownloadUrl = githubRelease.Assets.FirstOrDefault()?.BrowserDownloadUrl,
                        PublishedAt = githubRelease.PublishedAt,
                        ChangeLog = githubRelease.Body.Split('\n').ToList()
                    };
                }

                logger.LogInformation("Application is up to date");
                return null;
            } catch (Exception ex) {
                logger.LogError(ex, "Error checking for updates");
                return null;
            }
        }

        public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null) {
            try {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1L;
                string downloadPath = Path.Combine(Path.GetTempPath(), $"LogParserApp_{updateInfo.Version}.zip");

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                long totalRead = 0L;
                byte[] buffer = new byte[8192];
                bool isMoreDataToRead = true;

                do {
                    int read = await contentStream.ReadAsync(buffer);
                    if (read == 0) {
                        isMoreDataToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer.AsMemory(0, read));

                    totalRead += read;
                    progressCallback?.Report((int)(totalRead * 100 / totalBytes));
                } while (isMoreDataToRead);

                return downloadPath;
            } catch (Exception ex) {
                logger.LogError(ex, "Error downloading update");
                throw;
            }
        }

        public Task<bool> InstallUpdateAsync(string updateFilePath) {
            try {
                logger.LogInformation("Installing update from {UpdateFilePath}", updateFilePath);
                return Task.FromResult(true);
            } catch (Exception ex) {
                logger.LogError(ex, "Error installing update");
                return Task.FromResult(false);
            }
        }

        public Version? GetCurrentVersion() {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private class GitHubReleaseResponse
        {
            public string TagName { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public string Body { get; init; } = string.Empty;
            public DateTime PublishedAt { get; init; }
            public List<GitHubAsset> Assets { get; init; } = new List<GitHubAsset>();
        }

        private class GitHubAsset
        {
            public string? BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}