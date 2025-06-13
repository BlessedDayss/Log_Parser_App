namespace Log_Parser_App.Converters
{
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

	#region Class: LogLevelToBrushConverter

	public class LogLevelToBrushConverter : IValueConverter
	{

		#region Methods: Public

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is string levelString) {
				return levelString.ToUpperInvariant() switch {
					"ERROR" => new SolidColorBrush(Colors.Red),
					"WARNING" => new SolidColorBrush(Colors.Orange),
					"INFO" => new SolidColorBrush(Colors.CornflowerBlue),
					"DEBUG" => new SolidColorBrush(Colors.ForestGreen),
					"TRACE" => new SolidColorBrush(Colors.Gray),
					"CRITICAL" => new SolidColorBrush(Colors.DarkRed),
					"VERBOSE" => new SolidColorBrush(Colors.LightGray),
					_ => Brushes.Transparent // Or some default brush
				};
			}
			return Brushes.Transparent; // Default if value is not a string or is null
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			//ConvertBack not needed for this use case.
			throw new NotImplementedException();
		}

		#endregion

	}

	#endregion

}