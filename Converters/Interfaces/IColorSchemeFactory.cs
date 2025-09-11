namespace Log_Parser_App.Converters.Interfaces
{

    public interface IColorSchemeFactory
    {
        IColorProvider CreateColorProvider(string schemeName);
    }
}
