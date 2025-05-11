using System.Collections.Generic;
using System.Threading.Tasks;

namespace Log_Parser_App.Models.Interfaces
{
    public interface IFilePickerService
    {
        Task<IEnumerable<string>> PickFilesAsync();
        Task<string?> PickDirectoryAsync();
    }
} 