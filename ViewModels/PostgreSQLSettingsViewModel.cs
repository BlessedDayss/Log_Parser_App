using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    public partial class PostgreSQLSettingsViewModel : ObservableObject
    {
        private readonly IPostgreSQLSettingsService _settingsService;
        private readonly ILogger<PostgreSQLSettingsViewModel> _logger;

        [ObservableProperty]
        private string _host = "localhost";

        [ObservableProperty]
        private int _port = 5432;

        [ObservableProperty]
        private string _username = "postgres";

        [ObservableProperty]
        private string _password = "postgres";

        [ObservableProperty]
        private string _database = "log4net";

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private string _connectionStatus = "Not tested";

        [ObservableProperty]
        private bool _isTestingConnection = false;

        [ObservableProperty]
        private bool _isSaving = false;

        public string ConnectionString => $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};";

        public PostgreSQLSettingsViewModel(IPostgreSQLSettingsService settingsService, ILogger<PostgreSQLSettingsViewModel> logger)
        {
            _settingsService = settingsService;
            _logger = logger;
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                _logger.LogInformation("Loading PostgreSQL settings");
                var settings = await _settingsService.LoadSettingsAsync();
                
                Host = settings.Host;
                Port = settings.Port;
                Username = settings.Username;
                Password = settings.Password;
                Database = settings.Database;
                IsEnabled = settings.IsEnabled;
                
                _logger.LogInformation("PostgreSQL settings loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PostgreSQL settings");
                ConnectionStatus = $"Error loading settings: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task TestConnection()
        {
            if (IsTestingConnection) return;

            try
            {
                IsTestingConnection = true;
                ConnectionStatus = "Testing connection...";
                
                var settings = new PostgreSQLSettings
                {
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    Password = Password,
                    Database = Database,
                    IsEnabled = IsEnabled
                };

                _logger.LogInformation("Testing PostgreSQL connection to {Host}:{Port}", Host, Port);
                var success = await _settingsService.TestConnectionAsync(settings);
                
                if (success)
                {
                    ConnectionStatus = "✅ Connection successful!";
                    _logger.LogInformation("PostgreSQL connection test successful");
                }
                else
                {
                    ConnectionStatus = "❌ Connection failed!";
                    _logger.LogWarning("PostgreSQL connection test failed");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"❌ Error: {ex.Message}";
                _logger.LogError(ex, "Error testing PostgreSQL connection");
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            if (IsSaving) return;

            try
            {
                IsSaving = true;
                _logger.LogInformation("Saving PostgreSQL settings");
                
                var settings = new PostgreSQLSettings
                {
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    Password = Password,
                    Database = Database,
                    IsEnabled = IsEnabled
                };

                await _settingsService.SaveSettingsAsync(settings);
                
                ConnectionStatus = "✅ Settings saved successfully!";
                _logger.LogInformation("PostgreSQL settings saved successfully");
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"❌ Error saving: {ex.Message}";
                _logger.LogError(ex, "Error saving PostgreSQL settings");
            }
            finally
            {
                IsSaving = false;
            }
        }

        [RelayCommand]
        private async Task CreateDatabase()
        {
            try
            {
                ConnectionStatus = "Creating database...";
                
                var settings = new PostgreSQLSettings
                {
                    Host = Host,
                    Port = Port,
                    Username = Username,
                    Password = Password,
                    Database = Database,
                    IsEnabled = IsEnabled
                };

                _logger.LogInformation("Creating PostgreSQL database '{Database}'", Database);
                var success = await _settingsService.CreateDatabaseIfNotExistsAsync(settings);
                
                if (success)
                {
                    ConnectionStatus = "✅ Database created successfully!";
                    _logger.LogInformation("PostgreSQL database created successfully");
                }
                else
                {
                    ConnectionStatus = "❌ Failed to create database!";
                    _logger.LogWarning("Failed to create PostgreSQL database");
                }
            }
            catch (Exception ex)
            {
                ConnectionStatus = $"❌ Error creating database: {ex.Message}";
                _logger.LogError(ex, "Error creating PostgreSQL database");
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            _logger.LogInformation("Resetting PostgreSQL settings to defaults");
            
            Host = "localhost";
            Port = 5432;
            Username = "postgres";
            Password = "postgres";
            Database = "log4net";
            IsEnabled = true;
            ConnectionStatus = "Settings reset to defaults";
        }
    }
}


