using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Log_Parser_App.Converters.ColorSchemes;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters
{
	public class BoolToTabColorConverter : IValueConverter, IColorConverter
	{
		private readonly IColorScheme _colorScheme;

		public BoolToTabColorConverter()
		{
			_colorScheme = new TabColorScheme();
		}

		public BoolToTabColorConverter(IColorScheme colorScheme)
		{
			_colorScheme = colorScheme;
		}

		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is bool isSelected)
			{
				return _colorScheme.GetBrush(isSelected ? "SELECTED" : "UNSELECTED");
			}
			return _colorScheme.GetBrush("DEFAULT");
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
