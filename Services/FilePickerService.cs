namespace Log_Parser_App.Services
{
using System.Collections.Generic;
using System.Threading.Tasks;
using Log_Parser_App.Models.Interfaces;
using Avalonia.Controls;
using Avalonia.Platform.Storage;


    using System.Linq;

    public class FilePickerService(Window window) : IFilePickerService
    {

        public async Task<IEnumerable<string>> PickFilesAsync() {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true
            });
            return files.Select(file => file.Path.LocalPath).ToList();
        }

        public async Task<string?> PickDirectoryAsync() {
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }
    }
}