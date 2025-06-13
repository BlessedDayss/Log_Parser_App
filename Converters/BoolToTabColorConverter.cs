namespace Log_Parser_App.Converters
{
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

	#region Class: BoolToTabColorConverter

	public class BoolToTabColorConverter : IValueConverter
	{

		#region Methods: Public

		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is bool isSelected && isSelected) {
				return new SolidColorBrush(Color.Parse("#3A3B3E"));
			}
			return new SolidColorBrush(Color.Parse("#2A2B2D"));
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}

		#endregion

	}

	#endregion

}