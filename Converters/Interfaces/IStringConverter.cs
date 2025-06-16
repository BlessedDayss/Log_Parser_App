using System;
using System.Globalization;

namespace Log_Parser_App.Converters.Interfaces
{
    public interface IStringConverter
    {
        object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
        object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
    }
} 