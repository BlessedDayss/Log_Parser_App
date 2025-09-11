using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Log_Parser_App.Converters
{
    public class ErrorCountToColorConverter : IValueConverter
    {
        public static readonly ErrorCountToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var errorCountStr = value?.ToString() ?? "0";
            
            if (int.TryParse(errorCountStr, out var errorCount))
            {
                if (errorCount == 0)
                    return new SolidColorBrush(Color.Parse("#2E7D32")); // Green for no errors
                else if (errorCount <= 5)
                    return new SolidColorBrush(Color.Parse("#FF9800")); // Orange for few errors
                else if (errorCount <= 20)
                    return new SolidColorBrush(Color.Parse("#F44336")); // Red for many errors
                else
                    return new SolidColorBrush(Color.Parse("#B71C1C")); // Dark red for critical
            }

            return new SolidColorBrush(Color.Parse("#424242")); // Default gray
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ErrorCountToColorConverter does not support ConvertBack");
        }
    }
} 