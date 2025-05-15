namespace Log_Parser_App.Services
{
using System.Collections.Generic;
using System.Threading.Tasks;
using Models.Interfaces;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Linq;
using Avalonia.Input;
using Avalonia;
using System;
using Avalonia.VisualTree;

    public class FilePickerService : IFilePickerService
    {
        public FilePickerService()
        {
        }

        public async Task<IEnumerable<string>> PickFilesAsync(Window? window) {
            if (window == null || window.StorageProvider == null) return Enumerable.Empty<string>();
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true
            });
            return files.Select(file => file.Path.LocalPath).ToList();
        }

        public async Task<string?> PickDirectoryAsync(Window? window) {
            if (window == null || window.StorageProvider == null) return null;
            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }

        public async Task<(IEnumerable<string>? Files, string? Directory)> ShowFilePickerContextMenuAsync(Window? window)
        {
            if (window == null) return (null, null);

            try
            {
                var tcs = new TaskCompletionSource<(IEnumerable<string>? Files, string? Directory)>();
                var menu = new ContextMenu();
                
                var filesItem = new MenuItem { Header = "Select Files" };
                var folderItem = new MenuItem { Header = "Select Folder" };

                filesItem.Click += async (s, e) =>
                {
                    menu.Close();
                    try 
                    {
                        var files = await PickFilesAsync(window);
                        tcs.TrySetResult((files, null));
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                };

                folderItem.Click += async (s, e) =>
                {
                    menu.Close();
                    try
                    {
                        var dir = await PickDirectoryAsync(window);
                        tcs.TrySetResult((null, dir));
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                };

                menu.ItemsSource = new[] { filesItem, folderItem };
                menu.Open(window);
                return await tcs.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowFilePickerContextMenuAsync: {ex.Message}");
                var files = await PickFilesAsync(window);
                return (files, null);
            }
        }
    }
}