using System.Collections.Generic;
using Avalonia.Media;

namespace Log_Parser_App.Converters.Interfaces;

public interface IColorSchemeConfiguration
{
    string SchemeName { get; }
    IReadOnlyDictionary<string, IBrush> GetColors();
    IBrush GetDefaultColor();
}