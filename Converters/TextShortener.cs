using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters
{
    public class TextShortener : ITextShortener
    {
        public string ShortenText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
    }
} 