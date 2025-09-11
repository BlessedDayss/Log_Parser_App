namespace Log_Parser_App.Interfaces
{
using System.Collections.Generic;

	#region Interface: ILogFilesLoader

	public interface ILogFilesLoader
	{

		#region Methods: Public

		IAsyncEnumerable<(string filePath, string line)> LoadLinesAsync(IEnumerable<string> filePaths);

		IAsyncEnumerable<(string filePath, string line)> LoadLinesFromDirectoryAsync(string directoryPath, string searchPattern = "*.log");

		#endregion

	}

	#endregion

}
