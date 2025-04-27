using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters 
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
                return dto.ToString("o", culture); 
            }
            return string.Empty; 
        }
    }
} 