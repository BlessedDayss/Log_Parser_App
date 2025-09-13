using System.Globalization;
using Avalonia.Media;

namespace Log_Parser_App.Converters.Interfaces;

public interface IColorConverter<in TInput> : IConverter<TInput, IBrush?>
{
    new IBrush? Convert(TInput value, CultureInfo? culture = null);
}