using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Log_Parser_App.Services
{
    public interface IFileService
    {
        void InitializeTopLevel(TopLevel topLevel);
        Task<string?> PickLogFileAsync(string extension = "");
        Task<string?> PickSaveLocationAsync(string defaultFileName, string extension);
        Task<IEnumerable<string>> PickFilesOrFolderAsync();
    }
}