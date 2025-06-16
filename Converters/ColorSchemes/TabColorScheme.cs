using System.Collections.Generic;
using Avalonia.Media;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters.ColorSchemes
{
    public class TabColorScheme : IColorScheme
    {
        private readonly Dictionary<string, IBrush> _colors;

        public TabColorScheme()
        {
            _colors = new Dictionary<string, IBrush>
            {
                ["SELECTED"] = new SolidColorBrush(Color.Parse("#3A3B3E")),
                ["UNSELECTED"] = new SolidColorBrush(Color.Parse("#2A2B2D")),
                ["UPDATE_AVAILABLE"] = new SolidColorBrush(Color.Parse("#4CAF50")),
                ["DEFAULT"] = new SolidColorBrush(Color.Parse("#AAAAAA"))
            };
        }

        public IBrush GetBrush(string key)
        {
            return _colors.TryGetValue(key.ToUpperInvariant(), out var brush) 
                ? brush 
                : _colors["DEFAULT"];
        }

        public bool HasColor(string key)
        {
            return _colors.ContainsKey(key.ToUpperInvariant());
        }
    }
} 