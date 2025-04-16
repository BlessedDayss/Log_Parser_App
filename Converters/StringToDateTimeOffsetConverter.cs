using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace LogParserApp.Converters // Changed namespace
{
    public class StringToDateTimeOffsetConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && DateTimeOffset.TryParse(str, culture, DateTimeStyles.AssumeLocal, out var dto))
            {
                return dto;
            }
            return null; 
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {            
            if (value is DateTimeOffset dto)
            {
                // Using a common format that TryParse can handle by default
                return dto.ToString("o", culture); // ISO 8601 format
            }
            // Return empty string or null if the value is not a DateTimeOffset
            return string.Empty; 
        }
    }
} 