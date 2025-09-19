using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Filtering.Interfaces;

public interface IConfigurationValidator
{
    ValidationResult Validate(FilterConfiguration configuration);
    bool SupportsSchemaVersion(string version);
    bool HasInvalidFileNameCharacters(string name);
}


