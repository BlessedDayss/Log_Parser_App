using System.IO;
using System.Collections.Generic;
using System.Linq;
using Log_Parser_App.Models;
using Log_Parser_App.Services.Filtering.Interfaces;

namespace Log_Parser_App.Services.Filtering;

public class ConfigurationValidator : IConfigurationValidator
{
        public ValidationResult Validate(FilterConfiguration configuration)
        {
            if (configuration == null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { "Configuration cannot be null" }
                };
            }

            var result = configuration.Validate();

            if (!SupportsSchemaVersion(configuration.SchemaVersion))
            {
                result.Errors.Add($"Unsupported schema version: {configuration.SchemaVersion}");
                result.IsValid = false;
            }

            if (HasInvalidFileNameCharacters(configuration.Name))
            {
                result.Errors.Add("Configuration name contains invalid file name characters");
                result.IsValid = false;
            }

            return result;
        }

    public bool SupportsSchemaVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        return version == "1.0";
    }

    public bool HasInvalidFileNameCharacters(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var invalidChars = Path.GetInvalidFileNameChars();
        return name.Any(c => invalidChars.Contains(c));
    }
}
