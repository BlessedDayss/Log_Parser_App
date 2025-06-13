namespace Log_Parser_App.Models.Interfaces
{
using Log_Parser_App.Models;
using System.Collections.Generic;
using System.Threading;

	#region Interface: IIISLogParserService

	public interface IIISLogParserService
	{

		#region Methods: Public

		IAsyncEnumerable<IisLogEntry> ParseLogFileAsync(string filePath, CancellationToken cancellationToken);

		#endregion

	}

	#endregion

}