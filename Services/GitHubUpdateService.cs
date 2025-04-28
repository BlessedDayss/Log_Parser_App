using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Log_Parser_App.Models;
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
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates from GitHub...");
                
                // Получаем информацию о последнем релизе
                var releaseUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
                var response = await _httpClient.GetAsync(releaseUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to check for updates: {StatusCode}", response.StatusCode);
                    return new UpdateInfo();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("GitHub API response: {Content}", content);
                
                var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (releaseInfo == null)
                {
                    _logger.LogWarning("Failed to parse GitHub release info");
                    return new UpdateInfo();
                }
                
                // Диагностическая информация о десериализованном объекте
                _logger.LogInformation("Deserialized ReleaseInfo: Name={Name}, ZipballUrl={ZipballUrl}, TarballUrl={TarballUrl}",
                    releaseInfo.Name, releaseInfo.ZipballUrl, releaseInfo.TarballUrl);
                
                // Пытаемся получить версию из тега
                if (!Version.TryParse(releaseInfo.TagName.TrimStart('v'), out var latestVersion))
                {
                    _logger.LogWarning("Failed to parse version from tag name: {TagName}", releaseInfo.TagName);
                    return new UpdateInfo();
                }
                
                // For testing purposes, force update even if the current version is up to date.
                // var currentVersion = GetCurrentVersion();
                // if (latestVersion <= currentVersion)
                // {
                //     _logger.LogInformation("Application is up to date. Current version: {CurrentVersion}, Latest version: {LatestVersion}", 
                //         currentVersion, latestVersion);
                //     return new UpdateInfo();
                // }
                
                // Выбираем подходящий ассет для загрузки (обычно это .zip или .exe)
                var asset = releaseInfo.Assets.FirstOrDefault(a => 
                    a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
                
                if (asset == null)
                {
                    _logger.LogWarning("No suitable assets found in release");
                    return new UpdateInfo();
                }
                
                var downloadUrl = GetAssetDownloadUrl(releaseInfo);
                _logger.LogInformation("Selected download URL: {Url}", downloadUrl);
                
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    _logger.LogWarning("No valid download URL found in release");
                    
                    // Пробуем извлечь URL напрямую из JSON
                    try 
                    {
                        using var jsonDoc = JsonDocument.Parse(content);
                        var root = jsonDoc.RootElement;
                        
                        if (root.TryGetProperty("zipball_url", out var zipballUrlElement))
                        {
                            downloadUrl = zipballUrlElement.GetString();
                            _logger.LogInformation("Obtained zipball_url directly from JSON: {Url}", downloadUrl);
                        }
                        else if (root.TryGetProperty("tarball_url", out var tarballUrlElement))
                        {
                            downloadUrl = tarballUrlElement.GetString();
                            _logger.LogInformation("Obtained tarball_url directly from JSON: {Url}", downloadUrl);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing JSON for direct URL extraction");
                    }
                    
                    // Если URL все еще пустой, используем жестко закодированный URL в качестве запасного варианта
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        // Используем стандартный шаблон URL для GitHub releases
                        downloadUrl = $"https://github.com/{_owner}/{_repo}/archive/refs/tags/v{latestVersion}.zip";
                        _logger.LogInformation("Using hardcoded fallback URL: {Url}", downloadUrl);
                    }
                    
                    if (string.IsNullOrEmpty(downloadUrl))
                    {
                        return new UpdateInfo();
                    }
                }
                
                var updateInfo = new UpdateInfo
                {
                    Version = latestVersion,
                    ReleaseName = releaseInfo.Name,
                    ReleaseNotes = releaseInfo.Body,
                    DownloadUrl = downloadUrl,
                    FileSize = 0, // Размер файла будет неизвестен для zipball
                    PublishedAt = releaseInfo.PublishedAt,
                    RequiresRestart = true
                };
                
                _logger.LogInformation("Created UpdateInfo with URL: {Url}", updateInfo.DownloadUrl);
                
                // Парсим ChangeLog из описания релиза
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
                
                _logger.LogInformation("New version available: {Version}", updateInfo.Version);
                
                // Проверка перед возвратом
                if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    _logger.LogWarning("UpdateInfo.DownloadUrl is still empty before return! Using fallback URL.");
                    updateInfo.DownloadUrl = $"https://github.com/{_owner}/{_repo}/archive/refs/tags/v{latestVersion}.zip";
                    _logger.LogInformation("Final fallback URL set: {Url}", updateInfo.DownloadUrl);
                }
                
                return updateInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return new UpdateInfo();
            }
        }
        
        /// <inheritdoc/>
        public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null)
        {
            try
            {
                _logger.LogInformation("Starting download with URL: {Url}", updateInfo.DownloadUrl);
                
                if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                {
                    _logger.LogError("Download URL is empty in UpdateInfo");
                    
                    // Создаем запасной URL
                    var fallbackUrl = $"https://github.com/{_owner}/{_repo}/archive/refs/tags/v{updateInfo.Version}.zip";
                    _logger.LogInformation("Using fallback URL for downloading: {Url}", fallbackUrl);
                    updateInfo.DownloadUrl = fallbackUrl;
                    
                    if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
                    {
                        throw new InvalidOperationException("Download URL is empty and fallback creation failed");
                    }
                }
                
                // Проверяем, что URL абсолютный
                if (!Uri.TryCreate(updateInfo.DownloadUrl, UriKind.Absolute, out var uri))
                {
                    _logger.LogError("Invalid download URL format: {Url}", updateInfo.DownloadUrl);
                    throw new InvalidOperationException($"Invalid download URL: {updateInfo.DownloadUrl}");
                }
                
                _logger.LogInformation("Downloading update {Version} from {Url}", 
                    updateInfo.Version, updateInfo.DownloadUrl);
                
                // Создаем уникальное имя файла для загрузки
                var fileName = $"update-{updateInfo.Version}.zip";
                var filePath = Path.Combine(_tempFolder, fileName);
                
                // Если файл уже существует, удаляем его
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                
                // Загружаем файл с отображением прогресса
                using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                
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
                
                _logger.LogInformation("Download completed: {FilePath}", filePath);
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
                var versionFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VERSION.txt");
                
                if (File.Exists(versionFilePath))
                {
                    var versionString = File.ReadAllText(versionFilePath).Trim();
                    if (Version.TryParse(versionString, out var version))
                    {
                        _logger.LogInformation("Using version from VERSION.txt: {Version}", version);
                        return version;
                    }
                    
                    _logger.LogWarning("Failed to parse version from VERSION.txt: {Version}", versionString);
                }
                else
                {
                    _logger.LogWarning("VERSION.txt file not found at {Path}", versionFilePath);
                }
                
                // Если чтение из файла не удалось, используем версию из сборки
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version;
                _logger.LogInformation("Using assembly version: {Version}", assemblyVersion);
                return assemblyVersion ?? new Version(0, 0, 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current version");
                return new Version(0, 0, 0);
            }
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
    }
} 