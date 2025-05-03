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

namespace Log_Parser_App.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly ILogger<UpdateService> _logger;
        private const string UpdateCheckUrl = "https://api.github.com/repos/YourUsername/LogParserApp/releases/latest";

        public UpdateService(ILogger<UpdateService> logger)
        {
            _logger = logger;
        }

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "LogParserApp");

                var response = await httpClient.GetStringAsync(UpdateCheckUrl);
                var githubRelease = JsonSerializer.Deserialize<GitHubReleaseResponse>(response);

                if (githubRelease == null)
                {
                    _logger.LogWarning("No update information found");
                    return null;
                }

                var latestVersion = Version.Parse(githubRelease.TagName.TrimStart('v'));
                var currentVersion = GetCurrentVersion();

                if (latestVersion > currentVersion)
                {
                    return new UpdateInfo
                    {
                        Version = latestVersion,
                        ReleaseName = githubRelease.Name,
                        ReleaseNotes = githubRelease.Body,
                        DownloadUrl = githubRelease.Assets.FirstOrDefault()?.BrowserDownloadUrl,
                        PublishedAt = githubRelease.PublishedAt,
                        ChangeLog = githubRelease.Body.Split('\n').ToList()
                    };
                }

                _logger.LogInformation("Application is up to date");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return null;
            }
        }

        public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null)
        {
            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var downloadPath = Path.Combine(Path.GetTempPath(), $"LogParserApp_{updateInfo.Version}.zip");

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreDataToRead = true;

                do
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        isMoreDataToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, read);

                    totalRead += read;
                    progressCallback?.Report((int)((totalRead * 100) / totalBytes));
                }
                while (isMoreDataToRead);

                return downloadPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading update");
                throw;
            }
        }

        public async Task<bool> InstallUpdateAsync(string updateFilePath)
        {
            try
            {
                // Логика установки обновления
                _logger.LogInformation($"Installing update from {updateFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing update");
                return false;
            }
        }

        public Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private class GitHubReleaseResponse
        {
            public string TagName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public DateTime PublishedAt { get; set; }
            public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
        }

        private class GitHubAsset
        {
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
}
