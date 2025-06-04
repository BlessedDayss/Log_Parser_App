namespace Log_Parser_App.ViewModels
{
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models.Interfaces;
using Microsoft.Extensions.Logging;


	public partial class UpdateViewModel : ViewModelBase
	{
		private readonly IUpdateService _updateService;
		private readonly ILogger<UpdateViewModel> _logger;

		[ObservableProperty]
		private bool _isCheckingForUpdates;
		[ObservableProperty]
		private bool _isDownloadingUpdate;
		[ObservableProperty]
		private bool _isInstallingUpdate;
		[ObservableProperty]
		private int _downloadProgress;
		[ObservableProperty]
		private string _statusMessage = "Checking for updates...";
		[ObservableProperty]
		private UpdateInfo? _availableUpdate;

		public UpdateViewModel(IUpdateService updateService, ILogger<UpdateViewModel> logger)
		{
			_updateService = updateService;
			_logger = logger;
			CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
			DownloadAndUpdateCommand = new AsyncRelayCommand(DownloadUpdateAndInstallAsync, CanDownloadUpdateAndInstall);
		}

		public IAsyncRelayCommand CheckForUpdatesCommand { get; }
		public IAsyncRelayCommand DownloadAndUpdateCommand { get; }

		private bool IsUpdateValid() {
			return AvailableUpdate != null && AvailableUpdate.Version > new Version(0, 0, 0);
		}

		public async Task CheckForUpdatesOnStartupAsync() {
			try {
				_logger.LogInformation("Checking for updates on startup");
				await CheckForUpdatesAsync();
				if (IsUpdateValid()) {
					_logger.LogInformation("Update available: {Version}", AvailableUpdate?.Version);
				} else {
					AvailableUpdate = null;
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Error checking for updates on startup");
			}
		}

		public async Task AutoUpdateAsync() {
			try {
				_logger.LogInformation("Starting auto update process");
				await CheckForUpdatesAsync();
				if (AvailableUpdate == null) {
					_logger.LogInformation("No updates available");
					return;
				}
				string? filePath = await _updateService.DownloadUpdateAsync(AvailableUpdate, null);
				if (string.IsNullOrEmpty(filePath)) {
					_logger.LogWarning("Download failed or was cancelled");
					return;
				}
				await _updateService.InstallUpdateAsync(filePath);
			} catch (Exception ex) {
				_logger.LogError(ex, "Auto update failed");
				StatusMessage = $"Ошибка: {ex.Message}";
			}
		}

		private async Task CheckForUpdatesAsync() {
			if (IsCheckingForUpdates)
				return;
			IsCheckingForUpdates = true;
			StatusMessage = "Checking for updates...";
			try {
				AvailableUpdate = await _updateService.CheckForUpdatesAsync();
				if (AvailableUpdate != null && AvailableUpdate.Version != new Version(0, 0, 0) && AvailableUpdate.Version > _updateService.GetCurrentVersion()) {
					StatusMessage = $"Update available: {AvailableUpdate.Version}";
				} else {
					StatusMessage = "Installed version is up to date";
					AvailableUpdate = null;
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Failed to check for updates");
				StatusMessage = $"Failed to check for updates: {ex.Message}";
				AvailableUpdate = null;
			} finally {
				IsCheckingForUpdates = false;
				DownloadAndUpdateCommand.NotifyCanExecuteChanged();
			}
		}

		private bool CanDownloadUpdateAndInstall()
		{
			return AvailableUpdate != null && !IsCheckingForUpdates;
		}

		private async Task DownloadUpdateAndInstallAsync()
		{
			if (AvailableUpdate == null || string.IsNullOrEmpty(AvailableUpdate.DownloadUrl))
			{
				_logger.LogWarning("Download/Install called but AvailableUpdate or DownloadUrl is null.");
				StatusMessage = "Update information is missing.";
				return;
			}

			StatusMessage = "Downloading update...";
			DownloadProgress = 0;
			string? downloadedFilePath = null;
			try
			{
				var progressIndicator = new Progress<int>(percent => {
					DownloadProgress = percent;
					StatusMessage = $"Downloading update... {percent}%";
				});
				downloadedFilePath = await _updateService.DownloadUpdateAsync(AvailableUpdate, progressIndicator);

				if (string.IsNullOrEmpty(downloadedFilePath))
				{
					StatusMessage = "Failed to download update.";
					_logger.LogError("Downloaded file path is null or empty.");
					return;
				}

				StatusMessage = "Installing update...";
				DownloadProgress = 100; 

				bool installResult = await _updateService.InstallUpdateAsync(downloadedFilePath);
				if (installResult)
				{
					StatusMessage = "Update installed. Application will restart.";
					_logger.LogInformation("Update successfully initiated, application should restart.");
				} else {
					StatusMessage = "Failed to install update.";
					_logger.LogError("Update installation failed.");
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Error downloading or installing update");
				StatusMessage = $"Error during update process: {ex.Message}";
			}
		}
	}
}