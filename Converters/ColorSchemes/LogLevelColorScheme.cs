using System.Collections.Generic;
using Avalonia.Media;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters.ColorSchemes
{
    public class LogLevelColorScheme : IColorScheme
    {
        private readonly Dictionary<string, IBrush> _colors;

        public LogLevelColorScheme()
        {
            _colors = new Dictionary<string, IBrush>
            {
                ["ERROR"] = new SolidColorBrush(Colors.Red),
                ["WARNING"] = new SolidColorBrush(Colors.Orange),
                ["INFO"] = new SolidColorBrush(Colors.CornflowerBlue),
                ["DEBUG"] = new SolidColorBrush(Colors.ForestGreen),
                ["TRACE"] = new SolidColorBrush(Colors.Gray),
                ["CRITICAL"] = new SolidColorBrush(Colors.DarkRed),
                ["VERBOSE"] = new SolidColorBrush(Colors.LightGray)
            };
        }

        public IBrush GetBrush(string key)
        {
            return _colors.TryGetValue(key.ToUpperInvariant(), out var brush) 
                ? brush 
                : Brushes.Transparent;
        }

        public bool HasColor(string key)
        {
            return _colors.ContainsKey(key.ToUpperInvariant());
        }
    }
} 