using System;
using System.Globalization;
using Avalonia.Media;
using Log_Parser_App.Converters.Base;
using Log_Parser_App.Converters.ColorSchemes;
using Log_Parser_App.Converters.ColorSchemes.Configurations;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters;

public class LogLevelToBrushConverter : BaseTypedConverter<string, IBrush>, IColorConverter<string>
{
    private readonly IColorProvider _colorProvider;

    public LogLevelToBrushConverter()
        : this(new ColorSchemeFactory(), LogLevelColorSchemeConfiguration.SCHEME_NAME)
    {
    }

    public LogLevelToBrushConverter(IColorProvider colorProvider)
    {
        ArgumentNullException.ThrowIfNull(colorProvider);
        _colorProvider = colorProvider;
    }

    public LogLevelToBrushConverter(IColorSchemeFactory factory, string schemeName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);

        _colorProvider = factory.CreateColorProvider(schemeName);
    }

    public override IBrush Convert(string value, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return _colorProvider.GetDefaultBrush();

        return _colorProvider.GetBrushOrDefault(value);
    }

    protected override IBrush GetDefaultOutput() => _colorProvider.GetDefaultBrush();
}