using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public interface IFirstTimeSetupService
    {
        Task<bool> ShouldShowLog4NetSetupGuideAsync();
        Task SetLog4NetSetupGuideShownAsync();
    }

    public class FirstTimeSetupService : IFirstTimeSetupService
    {
        private readonly ILogger<FirstTimeSetupService> _logger;
        private readonly string _settingsFilePath;

        public FirstTimeSetupService(ILogger<FirstTimeSetupService> logger)
        {
            _logger = logger;
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogParserApp");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "first_time_setup.json");
        }

        public async Task<bool> ShouldShowLog4NetSetupGuideAsync()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    _logger.LogInformation("First time setup file does not exist, should show Log4Net setup guide");
                    return true;
                }

                var json = await File.ReadAllTextAsync(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<FirstTimeSetupSettings>(json);
                
                if (settings == null)
                {
                    _logger.LogWarning("Could not deserialize first time setup settings, showing guide");
                    return true;
                }

                var shouldShow = !settings.Log4NetSetupGuideShown;
                _logger.LogInformation("Should show Log4Net setup guide: {ShouldShow}", shouldShow);
                return shouldShow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if should show Log4Net setup guide, defaulting to show");
                return true;
            }
        }

        public async Task SetLog4NetSetupGuideShownAsync()
        {
            try
            {
                var settings = await LoadSettingsAsync();
                settings.Log4NetSetupGuideShown = true;
                settings.Log4NetSetupGuideShownDate = DateTime.UtcNow;

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json);
                
                _logger.LogInformation("Marked Log4Net setup guide as shown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving first time setup settings");
            }
        }

        private async Task<FirstTimeSetupSettings> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<FirstTimeSetupSettings>(json);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading first time setup settings");
            }

            return new FirstTimeSetupSettings();
        }
    }

    public class FirstTimeSetupSettings
    {
        public bool Log4NetSetupGuideShown { get; set; }
        public DateTime? Log4NetSetupGuideShownDate { get; set; }
    }
}


