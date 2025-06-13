namespace Log_Parser_App.Services
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Avalonia.Controls;
	using Avalonia.Platform.Storage;
	using Microsoft.Extensions.Logging;

	#region Interface: IFileService

	public interface IFileService
	{

		#region Methods: Public

		Task<string?> PickLogFileAsync(string extension = "");

		Task<string?> PickSaveLocationAsync(string defaultFileName, string extension);

		#endregion

	}

	#endregion

	#region Class: FileService

	public class FileService : IFileService
	{

		#region Fields: Private

		private readonly ILogger<FileService> _logger;
		private TopLevel? _topLevel;

		#endregion

		#region Constructors: Public

		public FileService(ILogger<FileService> logger, TopLevel? topLevel = null) {
			_logger = logger;
			_topLevel = topLevel;
		}

		#endregion

		#region Methods: Public

		public void InitializeTopLevel(TopLevel topLevel) {
			_topLevel = topLevel;
		}

		public async Task<string?> PickLogFileAsync(string extension = "") {
			if (_topLevel == null) {
				_logger.LogError("TopLevel not initialized");
				return null;
			}
			try {
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
				if (!string.IsNullOrEmpty(extension)) {
					extensions.Insert(0,
						new FilePickerFileType($"{extension.TrimStart('.')} Files") {
							Patterns = new[] { $"*{extension}" }
						});
				}
				var files = await _topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
					Title = "Select Log File",
					AllowMultiple = false,
					FileTypeFilter = extensions
				});
				if (files.Count == 0) {
					_logger.LogInformation("File selection cancelled");
					return null;
				}
				var file = files[0];
				_logger.LogInformation("Selected file: {FilePath}", file.Path.LocalPath);
				return file.Path.LocalPath;
			} catch (Exception ex) {
				_logger.LogError(ex, "Error opening file picker");
				return null;
			}
		}

		public async Task<string?> PickSaveLocationAsync(string defaultFileName, string extension) {
			try {
				if (_topLevel == null) {
					_topLevel = TopLevel.GetTopLevel(App.MainWindow);
					if (_topLevel == null) {
						_logger.LogError("TopLevel не был инициализирован");
						return null;
					}
				}
				var filetype = extension.ToLowerInvariant() switch {
					"csv" => new FilePickerFileType("CSV файл") { Patterns = new[] { "*.csv" } },
					"json" => new FilePickerFileType("JSON файл") { Patterns = new[] { "*.json" } },
					"xml" => new FilePickerFileType("XML файл") { Patterns = new[] { "*.xml" } },
					_ => new FilePickerFileType("Текстовый файл") { Patterns = new[] { "*.txt" } }
				};
				var filePickerOptions = new FilePickerSaveOptions {
					Title = "Сохранить файл",
					SuggestedFileName = defaultFileName,
					FileTypeChoices = new[] { filetype }
				};
				var storageProvider = _topLevel.StorageProvider;
				var result = await storageProvider.SaveFilePickerAsync(filePickerOptions);
				if (result == null)
					return null;
				return result.Path.LocalPath;
			} catch (Exception ex) {
				_logger.LogError(ex, "Ошибка при выборе места сохранения");
				return null;
			}
		}

		#endregion

	}

	#endregion

}