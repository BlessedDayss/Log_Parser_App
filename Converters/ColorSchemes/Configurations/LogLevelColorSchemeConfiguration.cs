using System.Collections.Generic;
using Avalonia.Media;
using Log_Parser_App.Converters.ColorSchemes.Base;

namespace Log_Parser_App.Converters.ColorSchemes.Configurations
{
    public class LogLevelColorSchemeConfiguration : BaseColorSchemeConfiguration
    {
        public const string SCHEME_NAME = "LogLevel";

        public const string ERROR_KEY = "ERROR";
        public const string WARNING_KEY = "WARNING";
        public const string INFO_KEY = "INFO";
        public const string DEBUG_KEY = "DEBUG";
        public const string TRACE_KEY = "TRACE";
        public const string CRITICAL_KEY = "CRITICAL";
        public const string VERBOSE_KEY = "VERBOSE";

        public LogLevelColorSchemeConfiguration() : base(SCHEME_NAME)
        {
        }

        protected override IReadOnlyDictionary<string, IBrush> BuildColorDictionary()
        {
            return new Dictionary<string, IBrush>
            {
                [ERROR_KEY] = CreateBrush(Colors.Red),
                [WARNING_KEY] = CreateBrush(Colors.Orange),
                [INFO_KEY] = CreateBrush(Colors.CornflowerBlue),
                [DEBUG_KEY] = CreateBrush(Colors.ForestGreen),
                [TRACE_KEY] = CreateBrush(Colors.Gray),
                [CRITICAL_KEY] = CreateBrush(Colors.DarkRed),
                [VERBOSE_KEY] = CreateBrush(Colors.LightGray)
            };
        }

        protected override IBrush BuildDefaultColor()
        {
            return Brushes.Transparent;
        }
    }
}
