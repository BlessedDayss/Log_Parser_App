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
using Microsoft.Extensions.Logging;

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
                var releaseInfo = JsonSerializer.Deserialize<GitHubReleaseInfo>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (releaseInfo == null)
                {
                    _logger.LogWarning("Failed to parse GitHub release info");
                    return new UpdateInfo();
                }
                
                // Пытаемся получить версию из тега
                if (!Version.TryParse(releaseInfo.TagName.TrimStart('v'), out var latestVersion))
                {
                    _logger.LogWarning("Failed to parse version from tag name: {TagName}", releaseInfo.TagName);
                    return new UpdateInfo();
                }
                
                // Проверяем, есть ли более новая версия
                var currentVersion = GetCurrentVersion();
                if (latestVersion <= currentVersion)
                {
                    _logger.LogInformation("Application is up to date. Current version: {CurrentVersion}, Latest version: {LatestVersion}", 
                        currentVersion, latestVersion);
                    return new UpdateInfo();
                }
                
                // Выбираем подходящий ассет для загрузки (обычно это .zip или .exe)
                var asset = releaseInfo.Assets.FirstOrDefault(a => 
                    a.Name.EndsWith(".zip") || a.Name.EndsWith(".exe"));
                
                if (asset == null)
                {
                    _logger.LogWarning("No suitable assets found in release");
                    return new UpdateInfo();
                }
                
                var updateInfo = new UpdateInfo
                {
                    Version = latestVersion,
                    ReleaseName = releaseInfo.Name,
                    ReleaseNotes = releaseInfo.Body,
                    DownloadUrl = asset.BrowserDownloadUrl,
                    FileSize = asset.Size,
                    PublishedAt = releaseInfo.PublishedAt,
                    RequiresRestart = true
                };
                
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
                using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
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
                
                // Здесь должна быть логика установки обновления
                // Обычно это распаковка ZIP-архива и перезапуск приложения
                
                // Пример базовой реализации:
                // 1. Извлечение архива во временную папку
                var extractPath = Path.Combine(_tempFolder, "extract");
                if (Directory.Exists(extractPath))
                {
                    Directory.Delete(extractPath, true);
                }
                Directory.CreateDirectory(extractPath);
                
                await Task.Run(() => System.IO.Compression.ZipFile.ExtractToDirectory(updateFilePath, extractPath));
                
                // 2. Подготовка установщика или скрипта обновления
                // В реальном приложении здесь должна быть сложная логика
                // с учетом платформы (Windows, macOS, Linux)
                
                // Для примера просто делаем заглушку
                _logger.LogInformation("Update extracted to {ExtractPath}", extractPath);
                
                // 3. Запуск установщика или скрипта обновления
                // Или замена файлов приложения и его перезапуск
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error installing update");
                return false;
            }
        }
        
        /// <inheritdoc/>
        public Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version ?? new Version(0, 0, 0);
        }
        
        // Вспомогательные классы для десериализации JSON ответа от GitHub API
        private class GitHubReleaseInfo
        {
            public string Url { get; set; } = string.Empty;
            public string TagName { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public bool Draft { get; set; }
            public bool Prerelease { get; set; }
            public DateTime PublishedAt { get; set; }
            public List<GitHubAsset> Assets { get; set; } = new List<GitHubAsset>();
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