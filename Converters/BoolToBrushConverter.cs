namespace Log_Parser_App.Converters
{
    using System;
    using System.Globalization;
    using Avalonia.Data.Converters;
    using Avalonia.Media;

    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is bool isTrue && parameter is string param) {
                return param switch {
                    "UpdateAvailable" => isTrue ? new SolidColorBrush(Color.Parse("#4CAF50")) : new SolidColorBrush(Color.Parse("#AAAAAA")),
                    _ => new SolidColorBrush(Color.Parse("#AAAAAA"))
                };
            }
            return new SolidColorBrush(Color.Parse("#AAAAAA"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
