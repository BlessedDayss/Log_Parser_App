using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Configuration model for auto-update settings
    /// </summary>
    public class AutoUpdateConfig
    {
        public bool Enabled { get; set; } = true;
        public int CheckIntervalHours { get; set; } = 1;
        public bool ShowNotifications { get; set; } = true;
        public bool AutoInstall { get; set; } = true;
        public RepositoryConfig Repository { get; set; } = new();
    }

    /// <summary>
    /// Repository configuration for GitHub updates
    /// </summary>
    public class RepositoryConfig
    {
        public string Owner { get; set; } = "BlessedDayss";
        public string Name { get; set; } = "Log_Parser_App";
    }

    /// <summary>
    /// Service for managing auto-update configuration
    /// </summary>
    public interface IAutoUpdateConfigService
    {
        /// <summary>
        /// Get current auto-update configuration
        /// </summary>
        Task<AutoUpdateConfig> GetConfigAsync();

        /// <summary>
        /// Save auto-update configuration
        /// </summary>
        Task SaveConfigAsync(AutoUpdateConfig config);

        /// <summary>
        /// Check if auto-update is enabled
        /// </summary>
        Task<bool> IsAutoUpdateEnabledAsync();

        /// <summary>
        /// Enable or disable auto-update
        /// </summary>
        Task SetAutoUpdateEnabledAsync(bool enabled);
    }

    /// <summary>
    /// Implementation of auto-update configuration service
    /// </summary>
    public class AutoUpdateConfigService : IAutoUpdateConfigService
    {
        private readonly ILogger<AutoUpdateConfigService> _logger;
        private readonly string _configFilePath;
        private AutoUpdateConfig? _cachedConfig;

        public AutoUpdateConfigService(ILogger<AutoUpdateConfigService> logger)
        {
            _logger = logger;
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDirectory = Path.Combine(appDataPath, "LogParserApp");
            Directory.CreateDirectory(appDirectory);
            _configFilePath = Path.Combine(appDirectory, "autoupdate.config.json");
        }

        /// <summary>
        /// Get current auto-update configuration
        /// </summary>
        public async Task<AutoUpdateConfig> GetConfigAsync()
        {
            try
            {
                if (_cachedConfig != null)
                {
                    return _cachedConfig;
                }

                if (File.Exists(_configFilePath))
                {
                    var json = await File.ReadAllTextAsync(_configFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<AutoUpdateConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (_cachedConfig != null)
                    {
                        _logger.LogDebug("Loaded auto-update config from: {ConfigPath}", _configFilePath);
                        return _cachedConfig;
                    }
                }

                // Return default config if file doesn't exist or parsing failed
                _cachedConfig = new AutoUpdateConfig();
                await SaveConfigAsync(_cachedConfig);
                
                _logger.LogInformation("Created default auto-update configuration");
                return _cachedConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading auto-update configuration, using defaults");
                return new AutoUpdateConfig();
            }
        }

        /// <summary>
        /// Save auto-update configuration
        /// </summary>
        public async Task SaveConfigAsync(AutoUpdateConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await File.WriteAllTextAsync(_configFilePath, json);
                _cachedConfig = config;

                _logger.LogDebug("Saved auto-update config to: {ConfigPath}", _configFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving auto-update configuration");
                throw;
            }
        }

        /// <summary>
        /// Check if auto-update is enabled
        /// </summary>
        public async Task<bool> IsAutoUpdateEnabledAsync()
        {
            try 
            {
                var config = await GetConfigAsync();
                var result = config.Enabled && config.AutoInstall;
                _logger.LogInformation("Auto-update enabled check: Enabled={Enabled}, AutoInstall={AutoInstall}, Result={Result}", 
                    config.Enabled, config.AutoInstall, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking auto-update enabled status");
                return false;
            }
        }

        /// <summary>
        /// Enable or disable auto-update
        /// </summary>
        public async Task SetAutoUpdateEnabledAsync(bool enabled)
        {
            var config = await GetConfigAsync();
            config.Enabled = enabled;
            config.AutoInstall = enabled;
            await SaveConfigAsync(config);
            
            _logger.LogInformation("Auto-update {Status}", enabled ? "enabled" : "disabled");
        }
    }
} 