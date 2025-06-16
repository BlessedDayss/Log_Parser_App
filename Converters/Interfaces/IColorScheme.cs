using Avalonia.Media;

namespace Log_Parser_App.Converters.Interfaces
{
    public interface IColorScheme
    {
        IBrush GetBrush(string key);
        bool HasColor(string key);
    }
} 