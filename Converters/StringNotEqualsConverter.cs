using System;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters
{
    public class StringNotEqualsConverter : IValueConverter
    {
        public static readonly StringNotEqualsConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var stringValue = value?.ToString() ?? string.Empty;
            var compareValue = parameter?.ToString() ?? string.Empty;
            
            // Handle multiple values separated by |
            var compareValues = compareValue.Split('|');
            
            return !compareValues.Any(cv => string.Equals(stringValue, cv, StringComparison.OrdinalIgnoreCase));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("StringNotEqualsConverter does not support ConvertBack");
        }
    }
} 