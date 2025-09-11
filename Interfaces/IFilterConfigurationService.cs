using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    /// <summary>
    /// Service interface for managing filter configuration persistence.
    /// Supports saving, loading, and validating filter configurations with versioning.
    /// </summary>
    public interface IFilterConfigurationService
    {
        /// <summary>
        /// Saves a filter configuration to persistent storage.
        /// </summary>
        /// <param name="configuration">Filter configuration to save</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task completing when configuration is saved</returns>
        Task SaveConfigurationAsync(FilterConfiguration configuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a filter configuration by name.
        /// </summary>
        /// <param name="name">Configuration name</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Loaded filter configuration or null if not found</returns>
        Task<FilterConfiguration?> LoadConfigurationAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available filter configuration names.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of configuration names</returns>
        Task<IEnumerable<string>> GetConfigurationNamesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes a filter configuration by name.
        /// </summary>
        /// <param name="name">Configuration name to delete</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if deleted successfully, false if not found</returns>
        Task<bool> DeleteConfigurationAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a filter configuration against the current schema.
        /// </summary>
        /// <param name="configuration">Configuration to validate</param>
        /// <returns>Validation result with errors if any</returns>
        ValidationResult ValidateConfiguration(FilterConfiguration configuration);

        /// <summary>
        /// Exports a filter configuration to JSON format.
        /// </summary>
        /// <param name="configuration">Configuration to export</param>
        /// <returns>JSON representation of the configuration</returns>
        string ExportConfigurationToJson(FilterConfiguration configuration);

        /// <summary>
        /// Imports a filter configuration from JSON format.
        /// </summary>
        /// <param name="json">JSON representation of configuration</param>
        /// <returns>Imported filter configuration</returns>
        FilterConfiguration ImportConfigurationFromJson(string json);

        /// <summary>
        /// Gets the current configuration schema version.
        /// </summary>
        string SchemaVersion { get; }

        /// <summary>
        /// Checks if the service supports a specific schema version.
        /// </summary>
        /// <param name="version">Schema version to check</param>
        /// <returns>True if version is supported</returns>
        bool SupportsSchemaVersion(string version);
    }
} 