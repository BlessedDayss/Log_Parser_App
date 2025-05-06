namespace Log_Parser_App.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Avalonia.Platform.Storage;
    using Microsoft.Extensions.Logging;

    public class FileService : IFileService
    {
        private readonly ILogger<FileService> _logger;
        private IStorageProvider? _storageProvider;

        public FileService(ILogger<FileService> logger)
        {
            _logger = logger;
        }

        public void InitializeTopLevel(TopLevel topLevel)
        {
            _storageProvider = topLevel.StorageProvider;
        }

        public async Task<string?> PickLogFileAsync(string extension = "")
        {
            if (_storageProvider == null)
            {
                _logger.LogError("StorageProvider not initialized");
                return null;
            }

            try
            {
                var extensions = new List<FilePickerFileType> {
                    new FilePickerFileType("Log Files") {
                        Patterns = new[] { "*.log", "*.txt", "*.csv" }
                    },
                    new FilePickerFileType("Text Files") {
                        Patterns = new[] { "*.txt" }
                    },
                    new FilePickerFileType("All Files") {
                        Patterns = new[] { "*.*" }
                    }
                };

                if (!string.IsNullOrEmpty(extension))
                {
                    extensions.Insert(0,
                        new FilePickerFileType($"{extension.TrimStart('.')} Files")
                        {
                            Patterns = new[] { $"*{extension}" }
                        });
                }

                var files = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Select Log File",
                    AllowMultiple = false,
                    FileTypeFilter = extensions
                });

                if (files.Count == 0)
                {
                    _logger.LogInformation("File selection cancelled");
                    return null;
                }

                var file = files[0];
                _logger.LogInformation("Selected file: {FilePath}", file.Path.LocalPath);

                return file.Path.LocalPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error opening file picker");
                return null;
            }
        }

        public async Task<string?> PickSaveLocationAsync(string defaultFileName, string extension)
        {
            try
            {
                if (_storageProvider == null)
                {
                    _logger.LogError("StorageProvider not initialized");
                    return null;
                }

                var filetype = extension.ToLowerInvariant() switch
                {
                    "csv" => new FilePickerFileType("CSV файл") { Patterns = new[] { "*.csv" } },
                    "json" => new FilePickerFileType("JSON файл") { Patterns = new[] { "*.json" } },
                    "xml" => new FilePickerFileType("XML файл") { Patterns = new[] { "*.xml" } },
                    _ => new FilePickerFileType("Текстовый файл") { Patterns = new[] { "*.txt" } }
                };

                var filePickerOptions = new FilePickerSaveOptions
                {
                    Title = "Сохранить файл",
                    SuggestedFileName = defaultFileName,
                    FileTypeChoices = new[] { filetype }
                };

                var result = await _storageProvider.SaveFilePickerAsync(filePickerOptions);

                if (result == null)
                    return null;

                return result.Path.LocalPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе места сохранения");
                return null;
            }
        }

        public async Task<IEnumerable<string>> PickFilesOrFolderAsync()
        {
            if (_storageProvider == null)
            {
                _logger.LogError("StorageProvider not initialized");
                return new List<string>();
            }

            var fileTypes = new FilePickerFileType[] {
                new("Log files") {
                    Patterns = new[] { "*.log", "*.txt", "*.csv" },
                    MimeTypes = new[] { "text/plain", "text/csv" }
                },
                new("All files") {
                    Patterns = new[] { "*.*" }
                }
            };

            var options = new FilePickerOpenOptions
            {
                Title = "Select Log Files",
                FileTypeFilter = fileTypes,
                AllowMultiple = true
            };

            var result = await _storageProvider.OpenFilePickerAsync(options);
            var paths = new List<string>();

            foreach (var file in result)
            {
                paths.Add(file.Path.LocalPath);
            }

            // If no files were selected, try to pick a folder
            if (paths.Count == 0)
            {
                var folderOptions = new FolderPickerOpenOptions
                {
                    Title = "Select Folder with Log Files",
                    AllowMultiple = false
                };

                var folders = await _storageProvider.OpenFolderPickerAsync(folderOptions);
                foreach (var folder in folders)
                {
                    paths.Add(folder.Path.LocalPath);
                }
            }

            return paths;
        }
    }
}