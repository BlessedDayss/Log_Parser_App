using System.Collections.Generic;

namespace Log_Parser_App.Services.Filtering.Interfaces;

public interface IFieldMetadataProvider
{
    IEnumerable<string> GetAvailableFields();
    IEnumerable<string> GetAvailableOperators(string fieldName);
    bool IsFieldSupported(string fieldName);
    bool IsOperatorSupported(string fieldName, string operatorName);
}
