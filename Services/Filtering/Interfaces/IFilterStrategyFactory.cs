using Log_Parser_App.Interfaces;

namespace Log_Parser_App.Services.Filtering.Interfaces;

public interface IFilterStrategyFactory<TEntry>
{
    IFilterStrategy<TEntry> CreateStrategy(string fieldName, string operatorName);
    bool IsFieldSupported(string fieldName);
    bool IsOperatorSupported(string fieldName, string operatorName);
}

