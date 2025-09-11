namespace Log_Parser_App.Converters
{
	using System;
	using System.Globalization;
	using Avalonia.Data.Converters;
	using Log_Parser_App.Converters.ColorSchemes;
	using Log_Parser_App.Converters.ColorSchemes.Configurations;
	using Log_Parser_App.Converters.Interfaces;

	public class BoolToTabColorConverter : IValueConverter, IColorConverter
	{
		private readonly IColorProvider _colorProvider;

		public BoolToTabColorConverter() {
			var factory = new ColorSchemeFactory();
			_colorProvider = factory.CreateColorProvider(TabColorSchemeConfiguration.SCHEME_NAME);
		}

		public BoolToTabColorConverter(IColorProvider colorProvider) {
			_colorProvider = colorProvider ?? throw new ArgumentNullException(nameof(colorProvider));
		}

		public BoolToTabColorConverter(IColorSchemeFactory factory, string schemeName) {
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));
			if (string.IsNullOrWhiteSpace(schemeName))
				throw new ArgumentException("Scheme name cannot be null or empty", nameof(schemeName));

			_colorProvider = factory.CreateColorProvider(schemeName);
		}

		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is bool isSelected) {
				return _colorProvider.GetBrush(isSelected ? TabColorSchemeConfiguration.SELECTED_KEY : TabColorSchemeConfiguration.UNSELECTED_KEY) ?? _colorProvider.GetDefaultBrush();
			}
			return _colorProvider.GetDefaultBrush();
		}

		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}