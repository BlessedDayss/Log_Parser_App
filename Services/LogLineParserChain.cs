namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using Log_Parser_App.Models;
    using Log_Parser_App.Models.Interfaces;


    public class LogLineParserChain : ILogLineParser
    {
        private readonly List<ILogLineParser> _parsers;

        public LogLineParserChain(IEnumerable<ILogLineParser> parsers) {
            _parsers = new List<ILogLineParser>(parsers);
        }

        public bool IsLogLine(string line) {
            foreach (var parser in _parsers)
                if (parser.IsLogLine(line))
                    return true;

            return false;
        }

        public LogEntry? Parse(string line, int lineNumber, string filePath) {
            foreach (var parser in _parsers) {
                if (parser.IsLogLine(line)) {
                    var entry = parser.Parse(line, lineNumber, filePath);
                    if (entry != null)
                        return entry;
                }
            }
            return null;
        }
    }
}