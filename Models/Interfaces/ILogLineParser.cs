namespace Log_Parser_App.Models.Interfaces
{
    public interface ILogLineParser
    {
        LogEntry? Parse(string line, int lineNumber, string filePath);
        bool IsLogLine(string line);
    }
} 