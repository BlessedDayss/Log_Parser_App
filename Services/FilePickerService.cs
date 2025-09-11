namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Log_Parser_App.Interfaces;
    using Avalonia.Controls;
    using Avalonia.Platform.Storage;
    using System.Linq;
    using System;
    using Avalonia.Interactivity;

    public class FilePickerService : IFilePickerService
    {
        public FilePickerService() {
        }

        public async Task<IEnumerable<string>> PickFilesAsync(Window? window) {
            if (window == null || window.StorageProvider == null)
                return Enumerable.Empty<string>();

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true
            });
            return files.Select(file => file.Path.LocalPath).ToList();
        }

        public async Task<string?> PickDirectoryAsync(Window? window) {
            if (window == null || window.StorageProvider == null)
                return null;

            var folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                AllowMultiple = false
            });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }

        public async Task<string?> SaveFileAsync(Window? window, string suggestedFileName = "export.csv") {
            if (window == null || window.StorageProvider == null)
                return null;

            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                SuggestedFileName = suggestedFileName,
                DefaultExtension = "csv",
                FileTypeChoices = new[] {
                    new FilePickerFileType("CSV files") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*" } }
                }
            });

            return file?.Path?.LocalPath;
        }

        public async Task<(IEnumerable<string>? Files, string? Directory)> ShowFilePickerContextMenuAsync(Window? window) {
            if (window == null)
                return (null, null);

            ContextMenu? menu = null;
            TaskCompletionSource<(IEnumerable<string>? Files, string? Directory)>? tcs = null;
            EventHandler<RoutedEventArgs>? menuClosedHandler = null;

            try {
                tcs = new TaskCompletionSource<(IEnumerable<string>? Files, string? Directory)>();
                menu = new ContextMenu();

                var filesItem = new MenuItem { Header = "Select Files" };
                var folderItem = new MenuItem { Header = "Select Folder" };

                menuClosedHandler = (s, e) => {
                    tcs.TrySetResult((null, null));
                    if (menu != null && menuClosedHandler != null) {
                        menu.Closed -= menuClosedHandler;
                    }
                };
                menu.Closed += menuClosedHandler;

                filesItem.Click += async (s, e) => {
                    if (menu != null && menuClosedHandler != null)
                        menu.Closed -= menuClosedHandler;
                    menu?.Close();
                    try {
                        var files = await PickFilesAsync(window);
                        tcs.TrySetResult((files, null));
                    } catch (Exception exClick) {
                        Console.WriteLine($"Error in PickFilesAsync: {exClick.Message}");
                        tcs.TrySetException(exClick);
                    }
                };

                folderItem.Click += async (s, e) => {
                    if (menu != null && menuClosedHandler != null)
                        menu.Closed -= menuClosedHandler;
                    menu?.Close();
                    try {
                        var dir = await PickDirectoryAsync(window);
                        tcs.TrySetResult((null, dir));
                    } catch (Exception exClick) {
                        Console.WriteLine($"Error in PickDirectoryAsync: {exClick.Message}");
                        tcs.TrySetException(exClick);
                    }
                };

                menu.ItemsSource = new[] { filesItem, folderItem };
                menu.Open(window);
                return await tcs.Task;
            } catch (Exception ex) {
                Console.WriteLine($"Error in ShowFilePickerContextMenuAsync: {ex.Message}");
                tcs?.TrySetException(ex);
                return (null, null);
            } finally {
                if (menu != null && menuClosedHandler != null) {
                    menu.Closed -= menuClosedHandler;
                }
            }
        }
    }
}
