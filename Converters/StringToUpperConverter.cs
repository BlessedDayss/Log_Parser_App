using System.Globalization;
using Log_Parser_App.Converters.Base;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters;

public class StringToUpperConverter : BaseTypedConverter<string?, string?>, IStringConverter
{
    public override string? Convert(string? value, CultureInfo? culture = null)
    {
        if (value == null)
            return null;

        return value.ToUpperInvariant();
    }

    protected override string? GetDefaultOutput() => null;
}
