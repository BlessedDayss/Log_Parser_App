using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Log_Parser_App.Models;
using Log_Parser_App.Models.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Сервис обновления приложения через GitHub
    /// </summary>
    public class GitHubUpdateService : IUpdateService
    {
        private readonly ILogger<GitHubUpdateService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _owner;
        private readonly string _repo;
        private readonly string _tempFolder;
        
        /// <summary>
        /// Конструктор
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="owner">Владелец репозитория</param>
        /// <param name="repo">Название репозитория</param>
        public GitHubUpdateService(ILogger<GitHubUpdateService> logger, string owner, string repo)
        {
            _logger = logger;
            _owner = owner;
            _repo = repo;
            
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("LogParserApp", GetCurrentVersion().ToString()));
                
            _tempFolder = Path.Combine(Path.GetTempPath(), "LogParserApp", "Updates");
            Directory.CreateDirectory(_tempFolder);
            
            _logger.LogInformation("GitHub update service initialized for {Owner}/{Repo}", owner, repo);
        }
        
        /// <inheritdoc/>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates from GitHub...");
                
                // First, verify we can access the GitHub repository and get the latest release
                var testResult = await TestGitHubConnectionAsync();
                if (!testResult.Success)
                {
                    _logger.LogWarning("GitHub connection test failed: {Message}", testResult.Message);
                    return null;
                }
                
                _logger.LogInformation("GitHub connection test successful. Latest release: {Tag}", testResult.TagName);
                
                // Get the current version
                var currentVersion = GetCurrentVersion();
                _logger.LogInformation("Current application version: {Version}", currentVersion);
                
                // Parse the version from the tag name
                string versionString = testResult.TagName.TrimStart('v');
                if (!Version.TryParse(versionString, out var latestVersion))
                {
                    _logger.LogWarning("Failed to parse version from tag name: {TagName}", testResult.TagName);
                    return null;
                }
                
                _logger.LogInformation("Latest version: {Version}", latestVersion);
                
                // Compare versions
                bool updateAvailable = latestVersion > currentVersion;
                _logger.LogInformation("Update available: {Available} (Current: {Current}, Latest: {Latest})",
                    updateAvailable, currentVersion, latestVersion);
                
                if (!updateAvailable)
                {
                    _logger.LogInformation("Application is up to date");
                    return null;
                }
                
                // Get detailed release info
                var releaseUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var response = await _httpClient.GetAsync(releaseUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to get detailed release info: {StatusCode}", response.StatusCode);
                    return null;
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (releaseInfo == null)
                {
                    _logger.LogWarning("Failed to parse GitHub release info");
                    return null;
                }
                
                // Verify the download URL
                string downloadUrl = testResult.DownloadUrl;
                
                // Create the update info
                var updateInfo = new UpdateInfo
                {
                    Version = latestVersion,
                    ReleaseName = releaseInfo.Name,
                    ReleaseNotes = releaseInfo.Body,
                    DownloadUrl = downloadUrl,
                    TagName = testResult.TagName,
                    PublishedAt = releaseInfo.PublishedAt,
                    RequiresRestart = true
                };
                
                // Parse the changelog from the release notes
                if (!string.IsNullOrEmpty(releaseInfo.Body))
                {
                    var lines = releaseInfo.Body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.TrimStart().StartsWith("-") || line.TrimStart().StartsWith("*"))
                        {
                            updateInfo.ChangeLog.Add(line.TrimStart('-', '*', ' '));
                        }
                    }
                }
                
                _logger.LogInformation("Update info created: Version={Version}, URL={Url}",
                    updateInfo.Version, updateInfo.DownloadUrl);
                
                return updateInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return null;
            }
        }
        
        /// <inheritdoc/>
        public async Task<string?> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null)
        {
            if (updateInfo == null)
            {
                _logger.LogError("UpdateInfo is null");
                throw new ArgumentNullException(nameof(updateInfo));
            }
            
            _logger.LogInformation("Starting download: Version={Version}, URL={Url}",
                updateInfo.Version, updateInfo.DownloadUrl);
            
            // Validate the download URL
            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                _logger.LogError("Download URL is empty");
                
                // Verify we can access the GitHub repository and get the latest release
                var testResult = await TestGitHubConnectionAsync();
                if (!testResult.Success)
                {
                    _logger.LogWarning("GitHub connection test failed: {Message}", testResult.Message);
                    throw new InvalidOperationException($"Unable to determine download URL: {testResult.Message}");
                }
                
                updateInfo.DownloadUrl = testResult.DownloadUrl;
                _logger.LogInformation("Using download URL from test: {Url}", updateInfo.DownloadUrl);
                
                if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    throw new InvalidOperationException("Unable to determine download URL");
                }
            }
            
            try
            {
                // Validate the URL
                if (!Uri.TryCreate(updateInfo.DownloadUrl, UriKind.Absolute, out var uri))
                {
                    _logger.LogError("Invalid download URL: {Url}", updateInfo.DownloadUrl);
                    throw new InvalidOperationException($"Invalid download URL: {updateInfo.DownloadUrl}");
                }
                
                // Test the URL before downloading
                _logger.LogInformation("Testing download URL: {Url}", updateInfo.DownloadUrl);
                var testResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, uri));
                
                if (!testResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Download URL is not accessible: {StatusCode}", testResponse.StatusCode);
                    throw new HttpRequestException($"Download URL is not accessible: {testResponse.StatusCode}");
                }
                
                // Create a unique file name for the download
                var fileName = $"update-{updateInfo.Version}.zip";
                if (!string.IsNullOrEmpty(updateInfo.TagName))
                {
                    fileName = $"update-{updateInfo.TagName.TrimStart('v')}.zip";
                }
                
                var filePath = Path.Combine(_tempFolder, fileName);
                
                // Delete the file if it already exists
                if (File.Exists(filePath))
                {
                    _logger.LogInformation("Deleting existing file: {Path}", filePath);
                    File.Delete(filePath);
                }
                
                // Download the file
                _logger.LogInformation("Downloading update to: {Path}", filePath);
                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to download update: {StatusCode}", response.StatusCode);
                    throw new HttpRequestException($"Response status code does not indicate success: {response.StatusCode}");
                }
                
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                var buffer = new byte[8192];
                var bytesRead = 0;
                var totalBytesRead = 0L;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    
                    totalBytesRead += bytesRead;
                    
                    if (totalBytes > 0 && progressCallback != null)
                    {
                        var percentComplete = (int)((totalBytesRead * 100) / totalBytes);
                        progressCallback.Report(percentComplete);
                    }
                }
                
                _logger.LogInformation("Download completed: {Path}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading update");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task<bool> InstallUpdateAsync(string updateFilePath)
        {
            try
            {
                _logger.LogInformation("Installing update from {FilePath}", updateFilePath);
                
                // 1. Извлечение архива во временную папку
                var extractPath = Path.Combine(_tempFolder, "extract");
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);
                
                await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(updateFilePath, extractPath));
                _logger.LogInformation("Update extracted to {ExtractPath}", extractPath);
                
                // 2. Находим корневую папку внутри распакованного архива
                // GitHub архивы обычно содержат вложенную папку с именем репозитория и версией
                var directories = Directory.GetDirectories(extractPath);
                var sourceDir = directories.Length > 0 ? directories[0] : extractPath;
                
                // 3. Определяем целевую директорию приложения
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                _logger.LogInformation("Application directory: {AppDir}", appDir);
                
                // 4. Создаем bat/sh скрипт для применения обновления после закрытия приложения
                var isWindows = OperatingSystem.IsWindows();
                var isMacOS = OperatingSystem.IsMacOS();
                var isLinux = OperatingSystem.IsLinux();
                
                _logger.LogInformation("Detected platform: Windows={isWindows}, macOS={isMacOS}, Linux={isLinux}", 
                    isWindows, isMacOS, isLinux);
                
                var scriptPath = string.Empty;
                
                if (isWindows)
                {
                    scriptPath = Path.Combine(_tempFolder, "update.bat");
                    var script = $@"@echo off
timeout /t 2 /nobreak > nul
xcopy ""{sourceDir}\*.*"" ""{appDir}"" /E /Y /I
start """" ""{Path.Combine(appDir, "Log_Parser_App.exe")}""
exit";
                    await File.WriteAllTextAsync(scriptPath, script);
                }
                else if (isMacOS || isLinux)
                {
                    scriptPath = Path.Combine(_tempFolder, "update.sh");
                    var logPath = Path.Combine(_tempFolder, "update_log.txt");
                    var executablePath = Path.Combine(appDir, "Log_Parser_App");
                    
                    var script = $@"#!/bin/bash
# Логирование для отладки
exec > ""{logPath}"" 2>&1
echo ""Starting update script at $(date)""
echo ""Waiting for application to close...""
sleep 5
echo ""Copying files from {sourceDir} to {appDir}""
cp -Rv ""{sourceDir}/"" ""{appDir}""
echo ""Setting executable permissions""
chmod +x ""{executablePath}""
echo ""Launching application""
open -W ""{executablePath}""
if [ $? -ne 0 ]; then
    echo ""Failed to open application with 'open' command, trying direct execution""
    cd ""{appDir}""
    ./""{Path.GetFileName(executablePath)}""
fi
echo ""Update completed at $(date)""";
                    
                    await File.WriteAllTextAsync(scriptPath, script);
                    _logger.LogInformation("Created update script at {ScriptPath} with content:\n{ScriptContent}", 
                        scriptPath, script);
                    
                    // Даем права на выполнение скрипта
                    await Task.Run(() => {
                        var chmod = new System.Diagnostics.Process
                        {
                            StartInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "chmod",
                                Arguments = $"+x \"{scriptPath}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            }
                        };
                        chmod.Start();
                        chmod.WaitForExit();
                        _logger.LogInformation("Set execute permissions on script: {ScriptPath}", scriptPath);
                    });
                }
                else
                {
                    _logger.LogError("Unsupported platform for automatic update");
                    return false;
                }
                
                // 5. Запускаем скрипт обновления и завершаем приложение
                _logger.LogInformation("Starting update script: {ScriptPath}", scriptPath);
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    UseShellExecute = true,
                    CreateNoWindow = false
                };
                
                if (isWindows)
                {
                    startInfo.FileName = scriptPath;
                }
                else if (isMacOS || isLinux)
                {
                    _logger.LogInformation("Using bash to execute script");
                    startInfo.FileName = "/bin/bash";
                    startInfo.Arguments = scriptPath;
                    startInfo.WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? _tempFolder;
                }
                
                var process = System.Diagnostics.Process.Start(startInfo);
                _logger.LogInformation("Update process started with ID: {ProcessId}", process?.Id ?? -1);
                
                // 6. Запрашиваем завершение приложения
                _logger.LogInformation("Requesting application shutdown for update installation");
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                
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
            try
            {
                // Get version directly from assembly attributes for more reliable version checking
                var assembly = Assembly.GetExecutingAssembly();
                
                // Try to get version from AssemblyInformationalVersionAttribute first
                var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                if (infoVersionAttr != null && Version.TryParse(infoVersionAttr.InformationalVersion, out var infoVersion))
                {
                    _logger.LogInformation("Using informational version: {Version}", infoVersion);
                    return infoVersion;
                }
                
                // Then try AssemblyFileVersionAttribute
                var fileVersionAttr = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
                if (fileVersionAttr != null && Version.TryParse(fileVersionAttr.Version, out var fileVersion))
                {
                    _logger.LogInformation("Using file version: {Version}", fileVersion);
                    return fileVersion;
                }
                
                // Finally fall back to assembly version
                var assemblyVersion = assembly.GetName().Version;
                if (assemblyVersion != null)
                {
                    _logger.LogInformation("Using assembly version: {Version}", assemblyVersion);
                    return assemblyVersion;
                }
                
                // Hardcoded version as last resort
                var hardcodedVersion = new Version(0, 1, 6);
                _logger.LogWarning("Using hardcoded version as fallback: {Version}", hardcodedVersion);
                return hardcodedVersion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current version");
                return new Version(0, 1, 6); // Return current version as fallback
            }
        }
        
        /// <summary>
        /// Tests the connection to GitHub and verifies we can access the latest release
        /// </summary>
        /// <returns>Test result with information about the latest release</returns>
        private async Task<GitHubTestResult> TestGitHubConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Testing GitHub connection to {Owner}/{Repo}", _owner, _repo);
                
                // First, check if the repository exists
                var repoUrl = $"https://api.github.com/repos/{_owner}/{_repo}";
                var repoResponse = await _httpClient.GetAsync(repoUrl);
                
                if (!repoResponse.IsSuccessStatusCode)
                {
                    return new GitHubTestResult 
                    { 
                        Success = false, 
                        Message = $"Repository not found: {repoResponse.StatusCode}" 
                    };
                }
                
                // Then check if there are any releases
                var releasesUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases";
                var releasesResponse = await _httpClient.GetAsync(releasesUrl);
                
                if (!releasesResponse.IsSuccessStatusCode)
                {
                    return new GitHubTestResult 
                    { 
                        Success = false, 
                        Message = $"Failed to get releases: {releasesResponse.StatusCode}" 
                    };
                }
                
                var content = await releasesResponse.Content.ReadAsStringAsync();
                var releases = JsonSerializer.Deserialize<List<GitHubReleaseInfo>>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (releases == null || !releases.Any())
                {
                    return new GitHubTestResult 
                    { 
                        Success = false, 
                        Message = "No releases found" 
                    };
                }
                
                // Get the latest release
                var latestRelease = releases.OrderByDescending(r => r.PublishedAt).First();
                
                // Verify the download URL
                string downloadUrl = "";
                
                // First try to find a suitable asset
                if (latestRelease.Assets != null && latestRelease.Assets.Any())
                {
                    var asset = latestRelease.Assets.FirstOrDefault(a => 
                        a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
                    
                    if (asset != null && !string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                    {
                        downloadUrl = asset.BrowserDownloadUrl;
                    }
                }
                
                // If no suitable asset found, use the zipball URL
                if (string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(latestRelease.ZipballUrl))
                {
                    downloadUrl = latestRelease.ZipballUrl;
                }
                
                // If still no URL, use the tarball URL
                if (string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(latestRelease.TarballUrl))
                {
                    downloadUrl = latestRelease.TarballUrl;
                }
                
                // If still no URL, construct one based on the tag name
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    downloadUrl = $"https://github.com/{_owner}/{_repo}/archive/refs/tags/{latestRelease.TagName}.zip";
                }
                
                // Test the download URL
                var downloadResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, downloadUrl));
                
                if (!downloadResponse.IsSuccessStatusCode)
                {
                    return new GitHubTestResult 
                    { 
                        Success = false, 
                        Message = $"Download URL is not accessible: {downloadResponse.StatusCode}" 
                    };
                }
                
                return new GitHubTestResult 
                { 
                    Success = true, 
                    TagName = latestRelease.TagName,
                    DownloadUrl = downloadUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing GitHub connection");
                return new GitHubTestResult 
                { 
                    Success = false, 
                    Message = $"Error: {ex.Message}" 
                };
            }
        }
        
        /// <summary>
        /// Result of testing the GitHub connection
        /// </summary>
        private class GitHubTestResult
        {
            /// <summary>
            /// Whether the test was successful
            /// </summary>
            public bool Success { get; set; }
            
            /// <summary>
            /// Message describing the result
            /// </summary>
            public string Message { get; set; } = string.Empty;
            
            /// <summary>
            /// Tag name of the latest release
            /// </summary>
            public string TagName { get; set; } = string.Empty;
            
            /// <summary>
            /// Download URL for the latest release
            /// </summary>
            public string DownloadUrl { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// Вспомогательный метод для получения URL для скачивания ассета
        /// </summary>
        /// <param name="release">Информация о релизе</param>
        /// <returns>URL для скачивания ассета</returns>
        private string GetAssetDownloadUrl(GitHubReleaseInfo release)
        {
            // Логируем значения URL для проверки
            _logger.LogInformation("ZipballUrl: {ZipballUrl}", release.ZipballUrl);
            _logger.LogInformation("TarballUrl: {TarballUrl}", release.TarballUrl);
            
            // Fallback download URL from GitHub release
            string fallbackUrl = !string.IsNullOrEmpty(release.ZipballUrl) ? release.ZipballUrl : release.TarballUrl;
            
            if (release.Assets == null || !release.Assets.Any())
            {
                _logger.LogInformation("No assets found in release, using fallback URL: {Url}", fallbackUrl);
                return fallbackUrl;
            }
            
            var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
            if (asset != null && !string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                _logger.LogInformation("Found suitable asset: {Name} with URL: {Url}", asset.Name, asset.BrowserDownloadUrl);
                return asset.BrowserDownloadUrl;
            }
            
            _logger.LogInformation("No suitable assets found, using fallback URL: {Url}", fallbackUrl);
            return fallbackUrl;
        }
        
        // Вспомогательные классы для десериализации JSON ответа от GitHub API
        private class GitHubReleaseInfo
        {
            [JsonPropertyName("tag_name")]
            public required string TagName { get; set; }

            [JsonPropertyName("name")]
            public required string Name { get; set; }

            [JsonPropertyName("assets")]
            public required List<GitHubAsset> Assets { get; set; }

            [JsonPropertyName("zipball_url")]
            public required string ZipballUrl { get; set; }

            [JsonPropertyName("tarball_url")]
            public required string TarballUrl { get; set; }

            [JsonPropertyName("body")]
            public required string Body { get; set; }

            [JsonPropertyName("published_at")]
            public required DateTime PublishedAt { get; set; }
        }
        
        private class GitHubAsset
        {
            public string Url { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ContentType { get; set; } = string.Empty;
            public long Size { get; set; }
            public string BrowserDownloadUrl { get; set; } = string.Empty;
        }
    }
} 