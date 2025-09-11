using Avalonia.Media;

namespace Log_Parser_App.Converters.Interfaces
{

    public interface IColorProvider
    {
        IBrush? GetBrush(string key);
        IBrush GetDefaultBrush();
    }
}
