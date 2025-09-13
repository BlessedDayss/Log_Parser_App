using System;
using System.Globalization;
using Avalonia.Media;
using Log_Parser_App.Converters.Base;
using Log_Parser_App.Converters.ColorSchemes;
using Log_Parser_App.Converters.ColorSchemes.Configurations;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters;

public class BoolToTabColorConverter : BaseTypedConverter<bool, IBrush>, IColorConverter<bool>
{
    private readonly IColorProvider _colorProvider;

    public BoolToTabColorConverter()
        : this(new ColorSchemeFactory(), TabColorSchemeConfiguration.SCHEME_NAME)
    {
    }

    public BoolToTabColorConverter(IColorProvider colorProvider)
    {
        ArgumentNullException.ThrowIfNull(colorProvider);
        _colorProvider = colorProvider;
    }

    public BoolToTabColorConverter(IColorSchemeFactory factory, string schemeName)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemeName);

        _colorProvider = factory.CreateColorProvider(schemeName);
    }

    public override IBrush Convert(bool value, CultureInfo? culture = null)
    {
        var key = value ? TabColorSchemeConfiguration.SELECTED_KEY : TabColorSchemeConfiguration.UNSELECTED_KEY;
        return _colorProvider.GetBrushOrDefault(key);
    }

    protected override IBrush GetDefaultOutput() => _colorProvider.GetDefaultBrush();
}