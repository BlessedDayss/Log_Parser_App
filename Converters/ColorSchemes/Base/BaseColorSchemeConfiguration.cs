namespace Log_Parser_App.Converters.ColorSchemes.Base
{
    using System;
    using System.Collections.Generic;
    using Avalonia.Media;
    using Log_Parser_App.Converters.Interfaces;


    public abstract class BaseColorSchemeConfiguration : IColorSchemeConfiguration
    {
        private readonly Lazy<IReadOnlyDictionary<string, IBrush>> _colors;
        private readonly Lazy<IBrush> _defaultColor;

        protected BaseColorSchemeConfiguration(string schemeName) {
            if (string.IsNullOrWhiteSpace(schemeName))
                throw new ArgumentException("Scheme name cannot be null or empty", nameof(schemeName));

            SchemeName = schemeName;

            _colors = new Lazy<IReadOnlyDictionary<string, IBrush>>(BuildColorDictionary);
            _defaultColor = new Lazy<IBrush>(BuildDefaultColor);
        }

        public string SchemeName { get; }

        public IReadOnlyDictionary<string, IBrush> GetColors() {
            return _colors.Value;
        }

        public IBrush GetDefaultColor() {
            return _defaultColor.Value;
        }

        protected abstract IReadOnlyDictionary<string, IBrush> BuildColorDictionary();

        protected virtual IBrush BuildDefaultColor() {
            return Brushes.Transparent;
        }

        protected static IBrush CreateBrush(string colorString) {
            try {
                return new SolidColorBrush(Color.Parse(colorString));
            } catch (FormatException) {
                return Brushes.Transparent;
            }
        }

        protected static IBrush CreateBrush(Color color) {
            return new SolidColorBrush(color);
        }
    }
}