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
using Log_Parser_App.Services.Filtering.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.Filtering
{
    public class FilterConfigurationService : IFilterConfigurationService
    {
        private readonly ILogger<FilterConfigurationService> _logger;
        private readonly IStorageProvider _storageProvider;
        private readonly IConfigurationValidator _validator;
        private readonly SemaphoreSlim _fileSemaphore;

        public string SchemaVersion => "1.0";

        public FilterConfigurationService(
            ILogger<FilterConfigurationService> logger,
            IStorageProvider storageProvider,
            IConfigurationValidator validator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _fileSemaphore = new SemaphoreSlim(1, 1);
        }

        public FilterConfigurationService(ILogger<FilterConfigurationService> logger)
            : this(logger, CreateDefaultStorage(), new ConfigurationValidator())
        {
        }

        private static IStorageProvider CreateDefaultStorage()
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LogParserApp",
                "FilterConfigurations");

            return new JsonFileStorageProvider(directory);
        }

        public async Task SaveConfigurationAsync(FilterConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var validation = _validator.Validate(configuration);
            if (!validation.IsValid)
            {
                var errors = string.Join(", ", validation.Errors);
                throw new ArgumentException($"Invalid configuration: {errors}");
            }

            configuration.LastModified = DateTimeOffset.UtcNow;
            if (configuration.CreatedAt == default)
                configuration.CreatedAt = DateTimeOffset.UtcNow;

            var json = JsonSerializer.Serialize(configuration, GetJsonOptions());
            await _storageProvider.SaveAsync(configuration.Name, json, cancellationToken);

            _logger.LogInformation("Saved filter configuration '{Name}'", configuration.Name);
        }

        public async Task<FilterConfiguration?> LoadConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configuration name cannot be empty", nameof(name));

            var json = await _storageProvider.LoadAsync(name, cancellationToken);
            if (json == null)
            {
                _logger.LogDebug("Configuration '{Name}' not found", name);
                return null;
            }

            var configuration = JsonSerializer.Deserialize<FilterConfiguration>(json, GetJsonOptions());
            if (configuration == null)
                return null;

            var validation = _validator.Validate(configuration);
                    if (!validation.IsValid)
                    {
                        _logger.LogWarning("Loaded configuration '{Name}' has validation errors: {Errors}",
                            configuration.Name, string.Join(", ", validation.Errors));
                }

            _logger.LogDebug("Loaded filter configuration '{Name}'", configuration.Name);
                return configuration;
        }

        public async Task<IEnumerable<string>> GetConfigurationNamesAsync(CancellationToken cancellationToken = default)
        {
            var keys = await _storageProvider.GetAllKeysAsync(cancellationToken);
            _logger.LogDebug("Found {Count} configuration files", keys.Count());
            return keys;
        }

        public async Task<bool> DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Configuration name cannot be empty", nameof(name));

            var deleted = await _storageProvider.DeleteAsync(name, cancellationToken);
            if (deleted)
                _logger.LogInformation("Deleted filter configuration '{Name}'", name);
            else
                _logger.LogDebug("Configuration '{Name}' not found for deletion", name);

            return deleted;
        }

        public ValidationResult ValidateConfiguration(FilterConfiguration configuration)
        {
            return _validator.Validate(configuration);
        }

        public string ExportConfigurationToJson(FilterConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            try
            {
                return JsonSerializer.Serialize(configuration, GetJsonOptions());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export configuration '{Name}' to JSON", configuration.Name);
                throw;
            }
        }

        public FilterConfiguration ImportConfigurationFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty", nameof(json));

            try
            {
                var configuration = JsonSerializer.Deserialize<FilterConfiguration>(json, GetJsonOptions());
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

        public bool SupportsSchemaVersion(string version)
        {
            return _validator.SupportsSchemaVersion(version);
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public void Dispose()
        {
            _fileSemaphore?.Dispose();
        }
    }
} 