using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Log_Parser_App.Converters
{
    public class StringShortenerConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                int maxLength = 50; // Default max length
                if (parameter is int pInt)
                {
                    maxLength = pInt;
                }
                else if (parameter is string pStr && int.TryParse(pStr, out int parsedInt))
                {
                    maxLength = parsedInt;
                }

                if (str.Length > maxLength)
                {
                    return str.Substring(0, maxLength - 3) + "...";
                }
                return str;
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 