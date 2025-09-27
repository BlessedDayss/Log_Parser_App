using Avalonia.Controls;
using Avalonia.Interactivity;
using Log_Parser_App.ViewModels;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Log_Parser_App.Views
{
    public partial class UpdateSettingsWindow : Window
    {
        private IPostgreSQLSettingsService? _postgreSQLSettingsService;
        private ILogger<UpdateSettingsWindow>? _logger;

        public UpdateSettingsWindow()
        {
            InitializeComponent();
            
            // Получаем сервисы из DI контейнера
            if (App.ServiceProvider != null)
            {
                var updateViewModel = App.ServiceProvider.GetRequiredService<UpdateViewModel>();
                DataContext = updateViewModel;
                
                _postgreSQLSettingsService = App.ServiceProvider.GetService<IPostgreSQLSettingsService>();
                _logger = App.ServiceProvider.GetService<ILogger<UpdateSettingsWindow>>();
                
                // Проверяем обновления при открытии окна
                _ = updateViewModel.CheckForUpdatesAsync();
                
                // Загружаем настройки PostgreSQL
                _ = LoadPostgreSQLSettingsAsync();
            }
            
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            // GitHub ссылка
            var gitHubLink = this.FindControl<TextBlock>("GitHubLink");
            if (gitHubLink != null)
            {
                gitHubLink.PointerPressed += GitHubLink_PointerPressed;
            }

            // PostgreSQL события
            var enableCheckBox = this.FindControl<CheckBox>("EnablePostgreSQLCheckBox");
            var testButton = this.FindControl<Button>("TestConnectionButton");
            var createButton = this.FindControl<Button>("CreateDatabaseButton");
            var saveButton = this.FindControl<Button>("SavePostgreSQLSettingsButton");
            var resetButton = this.FindControl<Button>("ResetPostgreSQLButton");
            var saveAllButton = this.FindControl<Button>("SaveAllButton");

            if (enableCheckBox != null)
                enableCheckBox.IsCheckedChanged += EnablePostgreSQL_CheckedChanged;
            
            if (testButton != null)
                testButton.Click += TestConnection_Click;
            
            if (createButton != null)
                createButton.Click += CreateDatabase_Click;
            
            if (saveButton != null)
                saveButton.Click += SavePostgreSQLSettings_Click;
            
            if (resetButton != null)
                resetButton.Click += ResetPostgreSQLSettings_Click;
                
            if (saveAllButton != null)
                saveAllButton.Click += SaveAllSettings_Click;

            // Обновление строки подключения при изменении полей
            var hostTextBox = this.FindControl<TextBox>("HostTextBox");
            var portNumericUpDown = this.FindControl<NumericUpDown>("PortNumericUpDown");
            var usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
            var passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
            var databaseTextBox = this.FindControl<TextBox>("DatabaseTextBox");

            if (hostTextBox != null) hostTextBox.TextChanged += UpdateConnectionString;
            if (portNumericUpDown != null) portNumericUpDown.ValueChanged += UpdateConnectionString;
            if (usernameTextBox != null) usernameTextBox.TextChanged += UpdateConnectionString;
            if (passwordTextBox != null) passwordTextBox.TextChanged += UpdateConnectionString;
            if (databaseTextBox != null) databaseTextBox.TextChanged += UpdateConnectionString;
        }

        private async Task LoadPostgreSQLSettingsAsync()
        {
            try
            {
                if (_postgreSQLSettingsService == null) return;

                _logger?.LogInformation("Loading PostgreSQL settings in UI");
                var settings = await _postgreSQLSettingsService.LoadSettingsAsync();

                var enableCheckBox = this.FindControl<CheckBox>("EnablePostgreSQLCheckBox");
                var hostTextBox = this.FindControl<TextBox>("HostTextBox");
                var portNumericUpDown = this.FindControl<NumericUpDown>("PortNumericUpDown");
                var usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
                var passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
                var databaseTextBox = this.FindControl<TextBox>("DatabaseTextBox");

                if (enableCheckBox != null) enableCheckBox.IsChecked = settings.IsEnabled;
                if (hostTextBox != null) hostTextBox.Text = settings.Host;
                if (portNumericUpDown != null) portNumericUpDown.Value = settings.Port;
                if (usernameTextBox != null) usernameTextBox.Text = settings.Username;
                if (passwordTextBox != null) passwordTextBox.Text = settings.Password;
                if (databaseTextBox != null) databaseTextBox.Text = settings.Database;

                UpdateConnectionString(null, null);
                UpdatePostgreSQLSettingsEnabled();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading PostgreSQL settings in UI");
                UpdateConnectionStatus("❌ Error loading settings");
            }
        }

        private void EnablePostgreSQL_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            UpdatePostgreSQLSettingsEnabled();
        }

        private void UpdatePostgreSQLSettingsEnabled()
        {
            var enableCheckBox = this.FindControl<CheckBox>("EnablePostgreSQLCheckBox");
            var settingsGrid = this.FindControl<Grid>("PostgreSQLSettingsGrid");
            var testButton = this.FindControl<Button>("TestConnectionButton");
            var createButton = this.FindControl<Button>("CreateDatabaseButton");

            var isEnabled = enableCheckBox?.IsChecked == true;

            if (settingsGrid != null) settingsGrid.IsEnabled = isEnabled;
            if (testButton != null) testButton.IsEnabled = isEnabled;
            if (createButton != null) createButton.IsEnabled = isEnabled;
        }

        private void UpdateConnectionString(object? sender, EventArgs? e)
        {
            var hostTextBox = this.FindControl<TextBox>("HostTextBox");
            var portNumericUpDown = this.FindControl<NumericUpDown>("PortNumericUpDown");
            var usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
            var passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
            var databaseTextBox = this.FindControl<TextBox>("DatabaseTextBox");

            var host = hostTextBox?.Text ?? "localhost";
            var port = portNumericUpDown?.Value ?? 5432;
            var username = usernameTextBox?.Text ?? "postgres";
            var password = passwordTextBox?.Text ?? "postgres";
            var database = databaseTextBox?.Text ?? "log4net";

            var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password=***";
            
            var connectionStringTextBlock = this.FindControl<TextBlock>("ConnectionStringTextBlock");
            if (connectionStringTextBlock != null)
            {
                connectionStringTextBlock.Text = $"Connection String: {connectionString}";
            }
        }

        private async void TestConnection_Click(object? sender, RoutedEventArgs e)
        {
            if (_postgreSQLSettingsService == null) return;

            var testButton = this.FindControl<Button>("TestConnectionButton");
            if (testButton != null) testButton.IsEnabled = false;

            try
            {
                UpdateConnectionStatus("🔄 Testing connection...");
                var settings = GetCurrentPostgreSQLSettings();
                
                _logger?.LogInformation("Testing PostgreSQL connection to {Host}:{Port} as {Username}", settings.Host, settings.Port, settings.Username);
                
                var success = await _postgreSQLSettingsService.TestConnectionAsync(settings);
                
                if (success)
                {
                    UpdateConnectionStatus("✅ Connection successful! PostgreSQL server is running and accessible.");
                }
                else
                {
                    UpdateConnectionStatus("❌ Connection failed! Check server status, credentials, and network connectivity.");
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger?.LogError(ex, "Network error testing PostgreSQL connection");
                UpdateConnectionStatus($"❌ Network Error: PostgreSQL server is not running or not accessible at {GetCurrentPostgreSQLSettings().Host}:{GetCurrentPostgreSQLSettings().Port}");
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger?.LogError(ex, "PostgreSQL error testing connection");
                string errorMessage = ex.SqlState switch
                {
                    "28P01" => "Authentication failed - check username and password",
                    "3D000" => "Database does not exist",
                    "08006" => "Connection failure - server may be down",
                    _ => $"PostgreSQL Error ({ex.SqlState}): {ex.Message}"
                };
                UpdateConnectionStatus($"❌ {errorMessage}");
            }
            catch (TimeoutException ex)
            {
                _logger?.LogError(ex, "Timeout testing PostgreSQL connection");
                UpdateConnectionStatus("❌ Connection timeout - server may be overloaded or network is slow");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error testing PostgreSQL connection");
                UpdateConnectionStatus($"❌ Unexpected Error: {ex.Message}");
            }
            finally
            {
                if (testButton != null) testButton.IsEnabled = true;
            }
        }

        private async void CreateDatabase_Click(object? sender, RoutedEventArgs e)
        {
            if (_postgreSQLSettingsService == null) return;

            try
            {
                UpdateConnectionStatus("Creating database...");
                var settings = GetCurrentPostgreSQLSettings();
                var success = await _postgreSQLSettingsService.CreateDatabaseIfNotExistsAsync(settings);
                
                UpdateConnectionStatus(success ? "✅ Database created successfully!" : "❌ Failed to create database!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating PostgreSQL database");
                UpdateConnectionStatus($"❌ Error creating database: {ex.Message}");
            }
        }

        private async void SavePostgreSQLSettings_Click(object? sender, RoutedEventArgs e)
        {
            if (_postgreSQLSettingsService == null) return;

            try
            {
                UpdateConnectionStatus("Saving settings...");
                var settings = GetCurrentPostgreSQLSettings();
                await _postgreSQLSettingsService.SaveSettingsAsync(settings);
                
                UpdateConnectionStatus("✅ Settings saved successfully!");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving PostgreSQL settings");
                UpdateConnectionStatus($"❌ Error saving: {ex.Message}");
            }
        }

        private void ResetPostgreSQLSettings_Click(object? sender, RoutedEventArgs e)
        {
            var hostTextBox = this.FindControl<TextBox>("HostTextBox");
            var portNumericUpDown = this.FindControl<NumericUpDown>("PortNumericUpDown");
            var usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
            var passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
            var databaseTextBox = this.FindControl<TextBox>("DatabaseTextBox");
            var enableCheckBox = this.FindControl<CheckBox>("EnablePostgreSQLCheckBox");

            if (hostTextBox != null) hostTextBox.Text = "localhost";
            if (portNumericUpDown != null) portNumericUpDown.Value = 5432;
            if (usernameTextBox != null) usernameTextBox.Text = "postgres";
            if (passwordTextBox != null) passwordTextBox.Text = "postgres";
            if (databaseTextBox != null) databaseTextBox.Text = "log4net";
            if (enableCheckBox != null) enableCheckBox.IsChecked = true;

            UpdateConnectionStatus("Settings reset to defaults");
        }

        private async void SaveAllSettings_Click(object? sender, RoutedEventArgs e)
        {
            // Сохраняем настройки PostgreSQL
            if (_postgreSQLSettingsService != null)
            {
                try
                {
                    UpdateConnectionStatus("Saving all settings...");
                    var settings = GetCurrentPostgreSQLSettings();
                    await _postgreSQLSettingsService.SaveSettingsAsync(settings);
                    UpdateConnectionStatus("✅ All settings saved successfully!");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error saving all settings");
                    UpdateConnectionStatus($"❌ Error saving: {ex.Message}");
                }
            }
            
            // Можно добавить сохранение других настроек здесь
        }

        private PostgreSQLSettings GetCurrentPostgreSQLSettings()
        {
            var hostTextBox = this.FindControl<TextBox>("HostTextBox");
            var portNumericUpDown = this.FindControl<NumericUpDown>("PortNumericUpDown");
            var usernameTextBox = this.FindControl<TextBox>("UsernameTextBox");
            var passwordTextBox = this.FindControl<TextBox>("PasswordTextBox");
            var databaseTextBox = this.FindControl<TextBox>("DatabaseTextBox");
            var enableCheckBox = this.FindControl<CheckBox>("EnablePostgreSQLCheckBox");

            return new PostgreSQLSettings
            {
                Host = hostTextBox?.Text ?? "localhost",
                Port = (int)(portNumericUpDown?.Value ?? 5432),
                Username = usernameTextBox?.Text ?? "postgres",
                Password = passwordTextBox?.Text ?? "postgres",
                Database = databaseTextBox?.Text ?? "log4net",
                IsEnabled = enableCheckBox?.IsChecked == true
            };
        }

        private void UpdateConnectionStatus(string status)
        {
            var statusTextBlock = this.FindControl<TextBlock>("ConnectionStatusTextBlock");
            if (statusTextBlock != null)
            {
                statusTextBlock.Text = status;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private void GitHubLink_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/BlessedDayss/Log_Parser_App",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Если не удалось открыть браузер, игнорируем ошибку
            }
        }
    }
} 