using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Interfaces;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    /// <summary>
    /// Service for managing filter configuration persistence using JSON file storage.
    /// Implements the IFilterConfigurationService interface with file-based storage.
    /// </summary>
    public class FilterConfigurationService : IFilterConfigurationService
    {
        private readonly ILogger<FilterConfigurationService> _logger;
        private readonly string _configurationDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _fileSemaphore;

        /// <summary>
        /// Current schema version supported by this service.
        /// </summary>
        public string SchemaVersion => "1.0";

        /// <summary>
        /// Initializes a new instance of FilterConfigurationService.
        /// </summary>
        /// <param name="logger">Logger for debugging and monitoring</param>
        /// <param name="configurationDirectory">Directory to store configuration files (optional)</param>
        public FilterConfigurationService(ILogger<FilterConfigurationService> logger, string? configurationDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configurationDirectory = configurationDirectory ?? GetDefaultConfigurationDirectory();
            _fileSemaphore = new SemaphoreSlim(1, 1);

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            // Ensure configuration directory exists
            EnsureConfigurationDirectoryExists();
        }

        /// <inheritdoc />
        public async Task SaveConfigurationAsync(FilterConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            // Validate configuration
            var validation = ValidateConfiguration(configuration);
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid configuration: {errors}");
            }

            // Update timestamps
            configuration.LastModified = DateTimeOffset.UtcNow;
            if (configuration.CreatedAt == default)
                configuration.CreatedAt = DateTimeOffset.UtcNow;

            var filePath = GetConfigurationFilePath(configuration.Name);

            await _fileSemaphore.WaitAsync(cancellationToken);
            try
            {
                var json = JsonSerializer.Serialize(configuration, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json, cancellationToken);
                
                _logger.LogInformation("Saved filter configuration '{Name}' to {FilePath}", 
                    configuration.Name, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration '{Name}' to {FilePath}", 
                    configuration.Name, filePath);
                throw;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<FilterConfiguration?> LoadConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configuration name cannot be empty", nameof(name));

            var filePath = GetConfigurationFilePath(name);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Configuration file not found: {FilePath}", filePath);
                return null;
            }

            await _fileSemaphore.WaitAsync(cancellationToken);
            try
            {
                var json = await File.ReadAllTextAsync(filePath, cancellationToken);
                var configuration = JsonSerializer.Deserialize<FilterConfiguration>(json, _jsonOptions);

                if (configuration != null)
                {
                    _logger.LogDebug("Loaded filter configuration '{Name}' from {FilePath}", 
                        configuration.Name, filePath);
                    
                    // Validate loaded configuration
                    var validation = ValidateConfiguration(configuration);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("Loaded configuration '{Name}' has validation errors: {Errors}",
                            configuration.Name, string.Join(", ", validation.Errors));
                    }
                }

                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration '{Name}' from {FilePath}", name, filePath);
                return null;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<string>> GetConfigurationNamesAsync(CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(_configurationDirectory))
                return Enumerable.Empty<string>();

            try
            {
                var configFiles = Directory.GetFiles(_configurationDirectory, "*.json");
                var names = configFiles
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();

                _logger.LogDebug("Found {Count} configuration files in {Directory}", 
                    names.Count, _configurationDirectory);

                return names!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get configuration names from {Directory}", _configurationDirectory);
                return Enumerable.Empty<string>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configuration name cannot be empty", nameof(name));

            var filePath = GetConfigurationFilePath(name);
            if (!File.Exists(filePath))
            {
                _logger.LogDebug("Configuration file not found for deletion: {FilePath}", filePath);
                return false;
            }

            await _fileSemaphore.WaitAsync(cancellationToken);
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted filter configuration '{Name}' from {FilePath}", name, filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete configuration '{Name}' from {FilePath}", name, filePath);
                return false;
            }
            finally
            {
                _fileSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public ValidationResult ValidateConfiguration(FilterConfiguration configuration)
        {
            if (configuration == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = { "Configuration cannot be null" }
                };
            }

            var result = configuration.Validate();

            // Additional service-level validations
            if (!SupportsSchemaVersion(configuration.SchemaVersion))
            {
                result.Errors.Add($"Unsupported schema version: {configuration.SchemaVersion}");
                result.IsValid = false;
            }

            // Validate file name compatibility
            if (HasInvalidFileNameCharacters(configuration.Name))
            {
                result.Errors.Add("Configuration name contains invalid file name characters");
                result.IsValid = false;
            }

            return result;
        }

        /// <inheritdoc />
        public string ExportConfigurationToJson(FilterConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            try
            {
                return JsonSerializer.Serialize(configuration, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration '{Name}' to JSON", configuration.Name);
                throw;
            }
        }

        /// <inheritdoc />
        public FilterConfiguration ImportConfigurationFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty", nameof(json));

            try
            {
                var configuration = JsonSerializer.Deserialize<FilterConfiguration>(json, _jsonOptions);
                if (configuration == null)
                    throw new InvalidOperationException("Deserialized configuration is null");

                _logger.LogDebug("Imported filter configuration '{Name}' from JSON", configuration.Name);
                return configuration;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import configuration from JSON");
                throw;
            }
        }

        /// <inheritdoc />
        public bool SupportsSchemaVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;

            // Currently only supporting version 1.0
            // Future versions can be added here with migration logic
            return version == "1.0";
        }

        /// <summary>
        /// Gets the default configuration directory.
        /// </summary>
        /// <returns>Default directory path for storing configurations</returns>
        private static string GetDefaultConfigurationDirectory()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataDir, "LogParserApp", "FilterConfigurations");
        }

        /// <summary>
        /// Ensures the configuration directory exists.
        /// </summary>
        private void EnsureConfigurationDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_configurationDirectory))
                {
                    Directory.CreateDirectory(_configurationDirectory);
                    _logger.LogInformation("Created configuration directory: {Directory}", _configurationDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create configuration directory: {Directory}", _configurationDirectory);
                throw;
            }
        }

        /// <summary>
        /// Gets the file path for a configuration by name.
        /// </summary>
        /// <param name="name">Configuration name</param>
        /// <returns>Full file path</returns>
        private string GetConfigurationFilePath(string name)
        {
            var fileName = $"{name}.json";
            return Path.Combine(_configurationDirectory, fileName);
        }

        /// <summary>
        /// Checks if a name contains invalid file name characters.
        /// </summary>
        /// <param name="name">Name to check</param>
        /// <returns>True if name contains invalid characters</returns>
        private static bool HasInvalidFileNameCharacters(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;

            var invalidChars = Path.GetInvalidFileNameChars();
            return name.Any(c => invalidChars.Contains(c));
        }

        /// <summary>
        /// Disposes resources used by this service.
        /// </summary>
        public void Dispose()
        {
            _fileSemaphore?.Dispose();
        }
    }
} 