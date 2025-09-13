using System.Globalization;

namespace Log_Parser_App.Converters.Interfaces;

public interface IVisibilityConverter<in TInput> : IConverter<TInput, bool>
{
    new bool Convert(TInput value, CultureInfo? culture = null);
}