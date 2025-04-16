using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace LogParserApp.Converters
{
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public static readonly EqualityToVisibilityConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isEqual = Equals(value?.ToString(), parameter?.ToString());
            return isEqual; // Directly return bool for IsVisible
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Not needed for IsVisible binding
            throw new NotSupportedException();
        }
    }
    
    // Optional: Converter to invert the boolean result for NotEqualTo logic
    public class InverseEqualityToVisibilityConverter : IValueConverter
    {
        public static readonly InverseEqualityToVisibilityConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isEqual = Equals(value?.ToString(), parameter?.ToString());
            return !isEqual; // Return inverted boolean
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        { 
            throw new NotSupportedException();
        }
    }
} 