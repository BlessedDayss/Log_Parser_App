namespace Log_Parser_App.Converters
{
	using System;
	using System.Globalization;
	using Avalonia.Data.Converters;
	using Log_Parser_App.Converters.ColorSchemes;
	using Log_Parser_App.Converters.ColorSchemes.Configurations;
	using Log_Parser_App.Converters.Interfaces;



	public class LogLevelToBrushConverter : IValueConverter, IColorConverter
	{
		private readonly IColorProvider _colorProvider;

		public LogLevelToBrushConverter() {
			var factory = new ColorSchemeFactory();
			_colorProvider = factory.CreateColorProvider(LogLevelColorSchemeConfiguration.SCHEME_NAME);
		}

		public LogLevelToBrushConverter(IColorProvider colorProvider) {
			_colorProvider = colorProvider ?? throw new ArgumentNullException(nameof(colorProvider));
		}

		public LogLevelToBrushConverter(IColorSchemeFactory factory, string schemeName) {
			if (factory == null)
				throw new ArgumentNullException(nameof(factory));
			if (string.IsNullOrWhiteSpace(schemeName))
				throw new ArgumentException("Scheme name cannot be null or empty", nameof(schemeName));

			_colorProvider = factory.CreateColorProvider(schemeName);
		}
		
		public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
			if (value is not string levelString)
				return _colorProvider.GetDefaultBrush();

			if (_colorProvider is ColorSchemes.Base.BaseColorProvider baseProvider) {
				return baseProvider.GetBrushOrDefault(levelString);
			}

			return _colorProvider.GetBrush(levelString) ?? _colorProvider.GetDefaultBrush();

		}
		
		public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}