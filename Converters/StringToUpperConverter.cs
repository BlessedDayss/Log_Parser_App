namespace Log_Parser_App.Converters
{
	using Avalonia.Data.Converters;
	using System;
	using System.Globalization;

	#region Class: StringToUpperConverter

	public class StringToUpperConverter : IValueConverter
	{

		#region Methods: Public

		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			if (value is string str) {
				return str.ToUpperInvariant();
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}

		#endregion

	}

	#endregion

}