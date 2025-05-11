using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return false;
            if (value is string str) return !string.IsNullOrWhiteSpace(str);
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
} 