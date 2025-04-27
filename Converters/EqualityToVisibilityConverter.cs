using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters
{
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public static readonly EqualityToVisibilityConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isEqual = Equals(value?.ToString(), parameter?.ToString());
            return isEqual; 
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
    
    public class InverseEqualityToVisibilityConverter : IValueConverter
    {
        public static readonly InverseEqualityToVisibilityConverter Instance = new();
        
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isEqual = Equals(value?.ToString(), parameter?.ToString());
            return !isEqual; 
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        { 
            throw new NotSupportedException();
        }
    }
} 