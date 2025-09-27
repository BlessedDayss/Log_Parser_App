using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Interfaces
{
    public interface ILog4NetParserService
    {
        Task<List<Log_Parser_App.Models.Log4NetLogEntry>> ParseLog4NetFileAsync(string filePath);
        Task<bool> ValidateLog4NetFileAsync(string filePath);
        LogFormatType GetLogFormatType();
    }
}