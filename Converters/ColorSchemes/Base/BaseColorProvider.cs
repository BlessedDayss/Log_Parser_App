namespace Log_Parser_App.Converters.ColorSchemes.Base
{
using System;
using System.Collections.Generic;
using Avalonia.Media;
using Log_Parser_App.Converters.Interfaces;


    public class BaseColorProvider : IColorProvider
    {
        private readonly IReadOnlyDictionary<string, IBrush> _colors;
        private readonly IBrush _defaultBrush;

        public BaseColorProvider(IColorSchemeConfiguration configuration, StringComparison keyComparison = StringComparison.OrdinalIgnoreCase) {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            _defaultBrush = configuration.GetDefaultColor();

            var colors = new Dictionary<string, IBrush>(keyComparison == StringComparison.OrdinalIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

            foreach (var kvp in configuration.GetColors()) {
                colors[kvp.Key] = kvp.Value;
            }

            _colors = colors;
        }

        public virtual IBrush? GetBrush(string key) {
            if (string.IsNullOrWhiteSpace(key))
                return _defaultBrush;

            return _colors.TryGetValue(key, out var brush) ? brush : null;
        }


        public virtual IBrush GetDefaultBrush() {
            return _defaultBrush;
        }

        public IBrush GetBrushOrDefault(string key) {
            return GetBrush(key) ?? GetDefaultBrush();
        }
    }
}