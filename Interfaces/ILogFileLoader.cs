namespace Log_Parser_App.Interfaces
{
using System.Collections.Generic;

	#region Interface: ILogFileLoader

	public interface ILogFileLoader
	{

		#region Methods: Public

		IAsyncEnumerable<string> LoadLinesAsync(string filePath);

		#endregion

	}

	#endregion

}
