namespace Log_Parser_App.Services
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class AutoUpdateConfig
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalHours { get; init; } = 1;
        public bool ShowNotifications { get; init; } = true;
        public bool AutoInstall { get; set; } = true;
        public RepositoryConfig Repository { get; init; } = new();
        public string? LastInstalledVersion { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
    }


    public class RepositoryConfig
    {
        public string Owner { get; set; } = "BlessedDayss";
        public string Name { get; set; } = "Log_Parser_App";
    }


    public interface IAutoUpdateConfigService
    {
        Task<AutoUpdateConfig> GetConfigAsync();

        Task SaveConfigAsync(AutoUpdateConfig config);

        Task<bool> IsAutoUpdateEnabledAsync();

        Task SetAutoUpdateEnabledAsync(bool enabled);

        Task<string?> GetLastInstalledVersionAsync();

        Task SetLastInstalledVersionAsync(string version);

        Task<bool> IsUpdateNeededAsync(Version currentVersion, Version availableVersion);
    }

    public class AutoUpdateConfigService : IAutoUpdateConfigService
    {
        private readonly ILogger<AutoUpdateConfigService> _logger;
        private readonly string _configFilePath;
        private AutoUpdateConfig? _cachedConfig;

        public AutoUpdateConfigService(ILogger<AutoUpdateConfigService> logger) {
            _logger = logger;
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appDirectory = Path.Combine(appDataPath, "LogParserApp");
            Directory.CreateDirectory(appDirectory);
            _configFilePath = Path.Combine(appDirectory, "autoupdate.config.json");
        }


        public async Task<AutoUpdateConfig> GetConfigAsync() {
            try {
                if (_cachedConfig != null) {
                    return _cachedConfig;
                }
                if (File.Exists(_configFilePath)) {
                    string json = await File.ReadAllTextAsync(_configFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<AutoUpdateConfig>(json,
                        new JsonSerializerOptions {
                            PropertyNameCaseInsensitive = true
                        });

                    if (_cachedConfig != null) {
                        _logger.LogDebug("Loaded auto-update config from: {ConfigPath}", _configFilePath);
                        return _cachedConfig;
                    }
                }
                _cachedConfig = new AutoUpdateConfig();
                await SaveConfigAsync(_cachedConfig);
                _logger.LogInformation("Created default auto-update configuration");
                return _cachedConfig;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error loading auto-update configuration, using defaults");
                return new AutoUpdateConfig();
            }
        }

        public async Task SaveConfigAsync(AutoUpdateConfig config) {
            try {
                string json = JsonSerializer.Serialize(config,
                    new JsonSerializerOptions {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                await File.WriteAllTextAsync(_configFilePath, json);
                _cachedConfig = config;

                _logger.LogDebug("Saved auto-update config to: {ConfigPath}", _configFilePath);
            } catch (Exception ex) {
                _logger.LogError(ex, "Error saving auto-update configuration");
                throw;
            }
        }


        public async Task<bool> IsAutoUpdateEnabledAsync() {
            try {
                var config = await GetConfigAsync();
                bool result = config.Enabled && config.AutoInstall;
                _logger.LogInformation("Auto-update enabled check: Enabled={Enabled}, AutoInstall={AutoInstall}, Result={Result}", config.Enabled, config.AutoInstall, result);
                return result;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error checking auto-update enabled status");
                return false;
            }
        }


        public async Task SetAutoUpdateEnabledAsync(bool enabled) {
            var config = await GetConfigAsync();
            config.Enabled = enabled;
            config.AutoInstall = enabled;
            await SaveConfigAsync(config);

            _logger.LogInformation("Auto-update {Status}", enabled ? "enabled" : "disabled");
        }

        public async Task<string?> GetLastInstalledVersionAsync() {
            var config = await GetConfigAsync();
            return config.LastInstalledVersion;
        }

        public async Task SetLastInstalledVersionAsync(string version) {
            var config = await GetConfigAsync();
            config.LastInstalledVersion = version;
            config.LastUpdateCheck = DateTime.Now;
            await SaveConfigAsync(config);

            _logger.LogInformation("Updated last installed version to: {Version}", version);
        }


        public async Task<bool> IsUpdateNeededAsync(Version currentVersion, Version availableVersion) {
            string? lastInstalled = await GetLastInstalledVersionAsync();
            if (string.IsNullOrEmpty(lastInstalled)) {
                return true; 
            }

            if (!Version.TryParse(lastInstalled, out var lastInstalledVersion))
                return true;

            if (currentVersion <= lastInstalledVersion)
                return availableVersion > lastInstalledVersion;
            await SetLastInstalledVersionAsync(currentVersion.ToString());
            return availableVersion > currentVersion;
        }
    }
}