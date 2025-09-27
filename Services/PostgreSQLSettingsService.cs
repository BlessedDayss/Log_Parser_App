using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services
{
    public class PostgreSQLSettingsService : IPostgreSQLSettingsService
    {
        private readonly ILogger<PostgreSQLSettingsService> _logger;
        private readonly string _settingsFilePath;

        public PostgreSQLSettingsService(ILogger<PostgreSQLSettingsService> logger)
        {
            _logger = logger;
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LogParserApp");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "postgresql_settings.json");
        }

        public async Task<PostgreSQLSettings> LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<PostgreSQLSettings>(json);
                    if (settings != null)
                    {
                        _logger.LogInformation("PostgreSQL settings loaded from {FilePath}", _settingsFilePath);
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading PostgreSQL settings from {FilePath}", _settingsFilePath);
            }

            _logger.LogInformation("Using default PostgreSQL settings");
            return GetDefaultSettings();
        }

        public async Task SaveSettingsAsync(PostgreSQLSettings settings)
        {
            try
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_settingsFilePath, json);
                _logger.LogInformation("PostgreSQL settings saved to {FilePath}", _settingsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving PostgreSQL settings to {FilePath}", _settingsFilePath);
                throw;
            }
        }

        public async Task<bool> TestConnectionAsync(PostgreSQLSettings settings)
        {
            try
            {
                _logger.LogInformation("Testing PostgreSQL connection to {Host}:{Port} with user {Username}", settings.Host, settings.Port, settings.Username);
                
                // Test with admin connection first (to postgres database)
                using var adminConnection = new NpgsqlConnection(settings.AdminConnectionString);
                await adminConnection.OpenAsync();
                
                // Execute a simple query to verify connection works
                using var testCmd = new NpgsqlCommand("SELECT version()", adminConnection);
                var version = await testCmd.ExecuteScalarAsync();
                
                _logger.LogInformation("PostgreSQL connection test successful. Version: {Version}", version);
                
                await adminConnection.CloseAsync();
                return true;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.LogError(ex, "PostgreSQL connection test failed - Database error: {Message}", ex.Message);
                return false;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                _logger.LogError(ex, "PostgreSQL connection test failed - Network error (server not running?): {Message}", ex.Message);
                return false;
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "PostgreSQL connection test failed - Timeout: {Message}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostgreSQL connection test failed - Unexpected error: {Message}", ex.Message);
                return false;
            }
        }

        public async Task<bool> CreateDatabaseIfNotExistsAsync(PostgreSQLSettings settings)
        {
            try
            {
                _logger.LogInformation("Checking if database '{Database}' exists", settings.Database);

                using var adminConnection = new NpgsqlConnection(settings.AdminConnectionString);
                await adminConnection.OpenAsync();

                // Check if database exists using parameterized query
                using var checkCmd = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @dbname", adminConnection);
                checkCmd.Parameters.AddWithValue("dbname", settings.Database);
                var exists = await checkCmd.ExecuteScalarAsync() != null;

                if (!exists)
                {
                    _logger.LogInformation("Creating database '{Database}' with owner '{Username}'", settings.Database, settings.Username);
                    
                    // Use quoted identifiers to handle special characters
                    using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{settings.Database}\" OWNER \"{settings.Username}\"", adminConnection);
                    await createCmd.ExecuteNonQueryAsync();
                    
                    _logger.LogInformation("Database '{Database}' created successfully", settings.Database);
                    
                    // Test connection to the newly created database
                    await adminConnection.CloseAsync();
                    
                    using var newDbConnection = new NpgsqlConnection(settings.ConnectionString);
                    await newDbConnection.OpenAsync();
                    await newDbConnection.CloseAsync();
                    
                    _logger.LogInformation("Successfully connected to newly created database '{Database}'", settings.Database);
                }
                else
                {
                    _logger.LogInformation("Database '{Database}' already exists", settings.Database);
                    
                    // Test connection to existing database
                    await adminConnection.CloseAsync();
                    
                    using var existingDbConnection = new NpgsqlConnection(settings.ConnectionString);
                    await existingDbConnection.OpenAsync();
                    await existingDbConnection.CloseAsync();
                    
                    _logger.LogInformation("Successfully connected to existing database '{Database}'", settings.Database);
                }

                return true;
            }
            catch (Npgsql.NpgsqlException ex) when (ex.SqlState == "42P04") // duplicate_database
            {
                _logger.LogInformation("Database '{Database}' already exists (concurrent creation)", settings.Database);
                return true;
            }
            catch (Npgsql.NpgsqlException ex)
            {
                _logger.LogError(ex, "PostgreSQL error creating database '{Database}': {SqlState} - {Message}", settings.Database, ex.SqlState, ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating database '{Database}': {Message}", settings.Database, ex.Message);
                return false;
            }
        }

        public PostgreSQLSettings GetDefaultSettings()
        {
            return new PostgreSQLSettings
            {
                Host = "localhost",
                Port = 5432,
                Username = "postgres",
                Password = "postgres",
                Database = "log4net",
                IsEnabled = true
            };
        }
    }
}
