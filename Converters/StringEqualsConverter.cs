using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters
{
    public class StringEqualsConverter : IValueConverter
    {
        public static readonly StringEqualsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var stringValue = value?.ToString() ?? string.Empty;
            var compareValue = parameter?.ToString() ?? string.Empty;
            
            return string.Equals(stringValue, compareValue, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringEqualsConverter does not support ConvertBack");
        }
    }
} 