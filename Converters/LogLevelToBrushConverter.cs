using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Log_Parser_App.Converters.ColorSchemes;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters
{
	public class LogLevelToBrushConverter : IValueConverter, IColorConverter
	{
		private readonly IColorScheme _colorScheme;

		public LogLevelToBrushConverter()
		{
			_colorScheme = new LogLevelColorScheme();
		}

		public LogLevelToBrushConverter(IColorScheme colorScheme)
		{
			_colorScheme = colorScheme;
		}

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is string levelString)
			{
				return _colorScheme.GetBrush(levelString);
			}
			return Brushes.Transparent;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
