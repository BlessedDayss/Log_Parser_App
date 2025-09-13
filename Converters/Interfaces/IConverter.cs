using System.Globalization;

namespace Log_Parser_App.Converters.Interfaces;

public interface IConverter<in TInput, out TOutput>
{
    TOutput Convert(TInput value, CultureInfo? culture = null);
}
