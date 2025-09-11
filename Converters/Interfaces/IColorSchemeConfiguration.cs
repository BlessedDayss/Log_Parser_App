namespace Log_Parser_App.Converters.Interfaces
{
    using System.Collections.Generic;
    using Avalonia.Media;

    public interface IColorSchemeConfiguration
    {
        IReadOnlyDictionary<string, IBrush> GetColors();
        IBrush GetDefaultColor();
        string SchemeName { get; }
    }
}