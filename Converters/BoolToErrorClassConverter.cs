using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Log_Parser_App.Converters
{
    public class BoolToErrorClassConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isError && isError)
            {
                return "error";
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 