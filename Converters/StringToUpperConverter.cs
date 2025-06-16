using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters
{
	public class StringToUpperConverter : IValueConverter, IStringConverter
	{
		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			if (value is string str)
			{
				return str.ToUpperInvariant();
			}
			return value;
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
