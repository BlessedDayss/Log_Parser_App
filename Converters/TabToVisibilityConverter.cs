namespace Log_Parser_App.Converters
{
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Log_Parser_App.Models;
using Log_Parser_App.ViewModels;

#region Class: TabToVisibilityConverter

	public class TabToVisibilityConverter : IValueConverter
	{

		#region Methods: Public

		public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is not TabViewModel tabViewModel || parameter is not string mode) {
				return false; // Or AvaloniaProperty.UnsetValue if more appropriate
			}
			return mode switch {
				"Standard" => tabViewModel.LogType == LogFormatType.Standard,
				"IIS" => tabViewModel.LogType == LogFormatType.IIS,
				_ => false
			};
		}

		public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}

		#endregion

	}

	#endregion

}
