namespace Log_Parser_App.Converters
{
    using System;
    using System.Globalization;
    using Avalonia.Data.Converters;


    public class StringShortenerConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value == null || parameter == null)
                return null;

            string parameterString = parameter.ToString() ?? string.Empty;

            // Проверяем, есть ли формат "string1|string2" для выбора по условию
            if (parameterString.Contains("|")) {
                string[] options = parameterString.Split('|');
                if (options.Length == 2) {
                    bool condition = value is bool boolVal && boolVal;
                    return condition ? options[0] : options[1];
                }
            }

            if (value is string strValue && !string.IsNullOrEmpty(strValue)) {
                int maxLength;
                if (int.TryParse(parameterString, out maxLength) && strValue.Length > maxLength) {
                    return strValue.Substring(0, maxLength) + "...";
                }
                return strValue;
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}