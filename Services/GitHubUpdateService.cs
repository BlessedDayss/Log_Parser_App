using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// GitHub release model for JSON deserialization
    /// </summary>
    public class GitHubRelease
    {
        public string tag_name { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string body { get; set; } = string.Empty;
        public DateTime published_at { get; set; }
        public bool prerelease { get; set; }
        public GitHubAsset[] assets { get; set; } = Array.Empty<GitHubAsset>();
    }

    /// <summary>
    /// GitHub asset model for JSON deserialization
    /// </summary>
    public class GitHubAsset 
    {
        public string name { get; set; } = string.Empty;
        public string browser_download_url { get; set; } = string.Empty;
        public string content_type { get; set; } = string.Empty;
        public long size { get; set; }
    }

    /// <summary>
    /// GitHub-based update service implementation
    /// </summary>
    public class GitHubUpdateService : IUpdateService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GitHubUpdateService> _logger;
        private readonly string _repositoryOwner;
        private readonly string _repositoryName;
        private readonly string _apiUrl;

        public GitHubUpdateService(ILogger<GitHubUpdateService> logger, string repositoryOwner = "BlessedDayss", string repositoryName = "Log_Parser_App")
        {
            _logger = logger;
            _repositoryOwner = repositoryOwner;
            _repositoryName = repositoryName;
            _apiUrl = $"https://api.github.com/repos/{_repositoryOwner}/{_repositoryName}";
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Log_Parser_App-Updater");
            
            _logger.LogInformation("GitHubUpdateService initialized for {Owner}/{Repo}", repositoryOwner, repositoryName);
        }

        /// <summary>
        /// Get current application version
        /// </summary>
        public Version GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version ?? new Version(1, 0, 0, 0);
                _logger.LogDebug("Current version: {Version}", version);
                return version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current version");
                return new Version(1, 0, 0, 0);
            }
        }

        /// <summary>
        /// Check for available updates from GitHub
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates from GitHub...");
                var response = await _httpClient.GetAsync($"{_apiUrl}/releases/latest");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("GitHub API request failed with status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                
                var release = JsonSerializer.Deserialize<GitHubRelease>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (release == null)
                {
                    _logger.LogWarning("Failed to deserialize GitHub release response");
                    return null;
                }

                // Skip pre-releases
                if (release.prerelease)
                {
                    _logger.LogInformation("Latest release is a pre-release, skipping");
                    return null;
                }

                // Parse version from tag
                var versionString = release.tag_name.TrimStart('v', 'V');
                
                if (!Version.TryParse(versionString, out var releaseVersion))
                {
                    _logger.LogWarning("Failed to parse version from tag: {Tag}", release.tag_name);
                    return null;
                }

                // Find appropriate asset for current platform
                var asset = FindPlatformAsset(release.assets);
                
                if (asset == null)
                {
                    _logger.LogWarning("No suitable asset found for current platform");
                    return null;
                }

                var updateInfo = new UpdateInfo
                {
                    Version = releaseVersion,
                    ReleaseName = release.name,
                    ReleaseNotes = release.body,
                    DownloadUrl = asset.browser_download_url,
                    TagName = release.tag_name,
                    PublishedAt = release.published_at,
                    RequiresRestart = true,
                    ChangeLog = ParseChangeLog(release.body)
                };

                _logger.LogInformation("Found release: {Version} published at {PublishedAt}", 
                    releaseVersion, release.published_at);

                return updateInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return null;
            }
        }

        /// <summary>
        /// Download update file from GitHub
        /// </summary>
        public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null)
        {
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                _logger.LogError("Download URL is empty");
                return null;
            }

            try
            {
                _logger.LogInformation("Downloading update from: {Url}", updateInfo.DownloadUrl);
                
                var tempPath = Path.GetTempPath();
                var fileName = Path.GetFileName(new Uri(updateInfo.DownloadUrl).LocalPath);
                var filePath = Path.Combine(tempPath, $"LogParser_Update_{updateInfo.Version}_{fileName}");

                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download update. Status: {StatusCode}", response.StatusCode);
                    return null;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);

                var buffer = new byte[8192];
                int bytesRead;
                
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progressPercentage = (int)((downloadedBytes * 100) / totalBytes);
                        progressCallback?.Report(progressPercentage);
                    }
                }

                _logger.LogInformation("Update downloaded successfully to: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading update");
                return null;
            }
        }

        /// <summary>
        /// Install downloaded update
        /// </summary>
        public async Task<bool> InstallUpdateAsync(string updateFilePath)
        {
            if (string.IsNullOrEmpty(updateFilePath) || !File.Exists(updateFilePath))
            {
                _logger.LogError("Update file not found: {FilePath}", updateFilePath);
                return false;
            }

            try
            {
                _logger.LogInformation("Installing update from: {FilePath}", updateFilePath);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return await InstallWindowsUpdateAsync(updateFilePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return await InstallLinuxUpdateAsync(updateFilePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return await InstallMacUpdateAsync(updateFilePath);
                }
                else
                {
                    _logger.LogError("Unsupported platform for auto-update");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing update");
                return false;
            }
        }

        /// <summary>
        /// Find appropriate asset for current platform
        /// </summary>
        private GitHubAsset? FindPlatformAsset(GitHubAsset[] assets)
        {
            if (assets == null || assets.Length == 0)
                return null;

            // Platform-specific asset selection
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return assets.FirstOrDefault(a => 
                    a.name.Contains("win", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return assets.FirstOrDefault(a => 
                    a.name.Contains("linux", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".rpm", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return assets.FirstOrDefault(a => 
                    a.name.Contains("mac", StringComparison.OrdinalIgnoreCase) ||
                    a.name.Contains("osx", StringComparison.OrdinalIgnoreCase) ||
                    a.name.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase));
            }

            // Fallback to first asset
            return assets.FirstOrDefault();
        }

        /// <summary>
        /// Parse changelog from release body
        /// </summary>
        private List<string> ParseChangeLog(string releaseBody)
        {
            if (string.IsNullOrEmpty(releaseBody))
                return new List<string>();

            var changes = new List<string>();
            var lines = releaseBody.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    changes.Add(trimmed.Substring(2));
                }
                else if (trimmed.StartsWith("## ") || trimmed.StartsWith("### "))
                {
                    changes.Add(trimmed);
                }
            }

            return changes;
        }

        /// <summary>
        /// Install update on Windows
        /// </summary>
        private async Task<bool> InstallWindowsUpdateAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                ProcessStartInfo startInfo;
                
                if (extension == ".exe")
                {
                    // Run installer executable
                    startInfo = new ProcessStartInfo
                    {
                        FileName = filePath,
                        Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                        UseShellExecute = true,
                        Verb = "runas" // Request admin privileges
                    };
                }
                else if (extension == ".msi")
                {
                    // Run MSI installer
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "msiexec",
                        Arguments = $"/i \"{filePath}\" /quiet /passive",
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                }
                else
                {
                    _logger.LogError("Unsupported Windows installer format: {Extension}", extension);
                    return false;
                }

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    _logger.LogInformation("Update installer started successfully");
                    
                    // Exit current application to allow update
                    await Task.Delay(2000); // Give installer time to start
                    Environment.Exit(0);
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing Windows update");
                return false;
            }
        }

        /// <summary>
        /// Install update on Linux
        /// </summary>
        private async Task<bool> InstallLinuxUpdateAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                ProcessStartInfo startInfo;

                if (extension == ".deb")
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "dpkg",
                        Arguments = $"-i \"{filePath}\"",
                        UseShellExecute = false
                    };
                }
                else if (extension == ".rpm")
                {
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "rpm",
                        Arguments = $"-Uvh \"{filePath}\"",
                        UseShellExecute = false
                    };
                }
                				else if (extension == ".appimage")
				{
					// Make AppImage executable and replace current application
					if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					{
						File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
					}
					
					var currentPath = Environment.ProcessPath;
					if (!string.IsNullOrEmpty(currentPath))
					{
						File.Copy(filePath, currentPath, true);
						_logger.LogInformation("AppImage updated successfully");
						Environment.Exit(0);
						return true;
					}
					return false;
				}
                else
                {
                    _logger.LogError("Unsupported Linux package format: {Extension}", extension);
                    return false;
                }

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    _logger.LogInformation("Linux package installer completed with exit code: {ExitCode}", process.ExitCode);
                    
                    if (process.ExitCode == 0)
                    {
                        Environment.Exit(0);
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing Linux update");
                return false;
            }
        }

        /// <summary>
        /// Install update on macOS
        /// </summary>
        private async Task<bool> InstallMacUpdateAsync(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                
                if (extension == ".dmg")
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false
                    };

                    var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        _logger.LogInformation("macOS DMG installer opened");
                        return true;
                    }
                }
                else
                {
                    _logger.LogError("Unsupported macOS installer format: {Extension}", extension);
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing macOS update");
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
