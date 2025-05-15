using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace Log_Parser_App.Models.Interfaces
{
    public interface IFilePickerService
    {
        Task<IEnumerable<string>> PickFilesAsync(Window? window);
        Task<string?> PickDirectoryAsync(Window? window);
        Task<(IEnumerable<string>? Files, string? Directory)> ShowFilePickerContextMenuAsync(Window? window);
    }
} 