using System.Globalization;

namespace Log_Parser_App.Converters.Interfaces;

public interface IStringConverter : IConverter<string?, string?>
{
    new string? Convert(string? value, CultureInfo? culture = null);
}