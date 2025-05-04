namespace Log_Parser_App.ViewModels
{
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;


	public partial class UpdateViewModel


(IUpdateService updateService, ILogger<UpdateViewModel> logger) : ViewModelBase
	{
		[ObservableProperty]
		private bool _isCheckingForUpdates;
		[ObservableProperty]
		private bool _isDownloadingUpdate;
		[ObservableProperty]
		private bool _isInstallingUpdate;
		[ObservableProperty]
		private int _downloadProgress;
		[ObservableProperty]
		private string _statusMessage = "Ready to check for updates";
		[ObservableProperty]
		private UpdateInfo? _availableUpdate;

		private bool IsUpdateValid() {
			return AvailableUpdate != null && AvailableUpdate.Version > new Version(0, 0, 0);
		}

		public async Task CheckForUpdatesOnStartupAsync() {
			try {
				logger.LogInformation("Checking for updates on startup");
				await CheckForUpdatesAsync();
				if (IsUpdateValid()) {
					logger.LogInformation("Update available: {Version}", AvailableUpdate?.Version);
				} else {
					AvailableUpdate = null;
				}
			} catch (Exception ex) {
				logger.LogError(ex, "Error checking for updates on startup");
			}
		}

		public async Task AutoUpdateAsync() {
			try {
				logger.LogInformation("Starting auto update process");
				await CheckForUpdatesAsync();
				if (AvailableUpdate == null) {
					logger.LogInformation("No updates available");
					return;
				}
				string filePath = await DownloadUpdateAsync();
				if (string.IsNullOrEmpty(filePath)) {
					logger.LogWarning("Download failed or was cancelled");
					return;
				}
				await InstallUpdateAsync(filePath);
			} catch (Exception ex) {
				logger.LogError(ex, "Auto update failed");
				StatusMessage = $"Ошибка: {ex.Message}";
			}
		}

		[RelayCommand]
		private async Task CheckForUpdates() {
			await CheckForUpdatesAsync();
		}

		private async Task CheckForUpdatesAsync() {
			if (IsCheckingForUpdates)
				return;
			IsCheckingForUpdates = true;
			StatusMessage = "Checking for updates...";
			try {
				AvailableUpdate = await updateService.CheckForUpdatesAsync();
				if (AvailableUpdate != null && AvailableUpdate.Version != new Version(0, 0, 0)) {
					StatusMessage = $"Update available: {AvailableUpdate.Version}";
				} else {
					StatusMessage = "Installed version is up to date";
					AvailableUpdate = null;
				}
			} catch (Exception ex) {
				logger.LogError(ex, "Failed to check for updates");
				StatusMessage = $"Failed to check for updates: {ex.Message}";
			} finally {
				IsCheckingForUpdates = false;
			}
		}

		[RelayCommand]
		private async Task DownloadUpdate() {
			await DownloadUpdateAsync();
		}

		private async Task<string> DownloadUpdateAsync() {
			if (IsDownloadingUpdate || AvailableUpdate == null)
				return string.Empty;
			IsDownloadingUpdate = true;
			DownloadProgress = 0;
			StatusMessage = "Downloading update...";
			try {
				var progress = new Progress<int>(percent => {
					DownloadProgress = percent;
					StatusMessage = $"Downloading update... {percent}%";
				});
				string filePath = await updateService.DownloadUpdateAsync(AvailableUpdate, progress);
				StatusMessage = "Download complete";
				return filePath;
			} catch (Exception ex) {
				logger.LogError(ex, "Failed to download update");
				StatusMessage = $"Failed: {ex.Message}";
				return string.Empty;
			} finally {
				IsDownloadingUpdate = false;
			}
		}

		[RelayCommand]
		private async Task InstallUpdate() {
			if (AvailableUpdate == null)
				return;
			var filePath = await DownloadUpdateAsync();
			if (string.IsNullOrEmpty(filePath))
				return;
			await InstallUpdateAsync(filePath);
		}

		private async Task InstallUpdateAsync(string filePath) {
			if (IsInstallingUpdate)
				return;
			IsInstallingUpdate = true;
			StatusMessage = "Installing update...";
			try {
				bool result = await updateService.InstallUpdateAsync(filePath);
				StatusMessage = result ? "Update installed successfully" : "Update installation failed";
			} catch (Exception ex) {
				logger.LogError(ex, "Failed to install update");
				StatusMessage = $"Failed: {ex.Message}";
			} finally {
				IsInstallingUpdate = false;
			}
		}

	}

}