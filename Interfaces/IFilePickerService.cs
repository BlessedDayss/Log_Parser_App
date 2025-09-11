namespace Log_Parser_App.Interfaces
{
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;

	#region Interface: IFilePickerService

	public interface IFilePickerService
	{

		#region Methods: Public

		Task<IEnumerable<string>> PickFilesAsync(Window? window);

		Task<string?> PickDirectoryAsync(Window? window);

		Task<string?> SaveFileAsync(Window? window, string suggestedFileName = "export.csv");

		Task<(IEnumerable<string>? Files, string? Directory)> ShowFilePickerContextMenuAsync(Window? window);

		#endregion

	}

	#endregion

}
