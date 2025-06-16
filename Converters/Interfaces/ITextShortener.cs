namespace Log_Parser_App.Converters.Interfaces
{
    public interface ITextShortener
    {
        string ShortenText(string text, int maxLength);
    }
} 