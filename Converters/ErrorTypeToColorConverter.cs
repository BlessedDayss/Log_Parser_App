using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Log_Parser_App.Converters
{
    public class ErrorTypeToColorConverter : IValueConverter
    {
        public static readonly ErrorTypeToColorConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var errorTypesStr = value?.ToString()?.ToLowerInvariant() ?? string.Empty;
            
            if (string.IsNullOrEmpty(errorTypesStr) || errorTypesStr == "none" || errorTypesStr == "0")
                return new SolidColorBrush(Color.Parse("#2E7D32")); // Green for no errors

            // Database errors (highest priority)
            if (errorTypesStr.Contains("dboperationexception") || errorTypesStr.Contains("postgresexception"))
                return new SolidColorBrush(Color.Parse("#7B1FA2")); // Purple for database errors

            // Critical errors
            if (errorTypesStr.Contains("exception"))
                return new SolidColorBrush(Color.Parse("#F44336")); // Red for exceptions

            // General errors
            if (errorTypesStr.Contains("error"))
                return new SolidColorBrush(Color.Parse("#FF5722")); // Deep orange for errors

            // Validation errors
            if (errorTypesStr.Contains("invalid") || errorTypesStr.Contains("rootalreadyexists"))
                return new SolidColorBrush(Color.Parse("#FF9800")); // Orange for validation errors

            // Mixed or other error types
            return new SolidColorBrush(Color.Parse("#795548")); // Brown for mixed/other errors
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ErrorTypeToColorConverter does not support ConvertBack");
        }
    }
} 