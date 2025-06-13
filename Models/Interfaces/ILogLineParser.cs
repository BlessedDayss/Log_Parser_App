namespace Log_Parser_App.Models.Interfaces
{

	#region Interface: ILogLineParser

	public interface ILogLineParser
	{

		#region Methods: Public

		LogEntry? Parse(string line, int lineNumber, string filePath);

		bool IsLogLine(string line);

		#endregion

	}

	#endregion

}