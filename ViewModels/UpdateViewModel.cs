namespace Log_Parser_App.ViewModels
{
	using System;
	using System.Reflection;
	using System.Threading.Tasks;
	using Avalonia.Threading;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using Log_Parser_App.Interfaces;
	using Log_Parser_App.Services;
	using Log_Parser_App.Views;
	using Microsoft.Extensions.Logging;

	public partial class UpdateViewModel : ViewModelBase
	{
		private readonly IUpdateService _updateService;
		private readonly IAutoUpdateConfigService _configService;
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
		private string _statusMessage = "Ready to check for updates";

		[ObservableProperty]
		private UpdateInfo? _availableUpdate;

		[ObservableProperty]
		private bool _autoUpdateEnabled = true;

		public string CurrentVersion {
			get {
				var version = Assembly.GetExecutingAssembly().GetName().Version;
				if (version != null) {
					return $"v{version.Major}.{version.Minor}.{version.Build}";
				}
				return "v1.0.0";
			}
		}

		public UpdateViewModel(IUpdateService updateService, IAutoUpdateConfigService configService, ILogger<UpdateViewModel> logger) {
			_updateService = updateService;
			_configService = configService;
			_logger = logger;
			CheckForUpdatesCommand = new AsyncRelayCommand(CheckForUpdatesAsync);
			DownloadAndUpdateCommand = new AsyncRelayCommand(DownloadUpdateAndInstallAsync, () => CanDownloadUpdateAndInstallAsync().GetAwaiter().GetResult());
			SetAutoUpdateEnabledCommand = new AsyncRelayCommand<bool>(SetAutoUpdateEnabledAsync);

			InitializeVersionTrackingAsync().ConfigureAwait(false);
		}

		public IAsyncRelayCommand CheckForUpdatesCommand { get; }
		public IAsyncRelayCommand DownloadAndUpdateCommand { get; }
		public IAsyncRelayCommand<bool> SetAutoUpdateEnabledCommand { get; }

		private async Task InitializeVersionTrackingAsync() {
			try {
				var currentVersion = _updateService.GetCurrentVersion();
				await _configService.SetLastInstalledVersionAsync(currentVersion.ToString());
				_logger.LogInformation("Initialized version tracking with current version: {Version}", currentVersion);
			} catch (Exception ex) {
				_logger.LogError(ex, "Error initializing version tracking");
			}
		}


		private async Task<bool> IsUpdateAvailableAsync() {
			if (AvailableUpdate == null || AvailableUpdate.Version == null)
				return false;

			var currentVersion = _updateService.GetCurrentVersion();
			return await _configService.IsUpdateNeededAsync(currentVersion, AvailableUpdate.Version);
		}


		private bool IsVersionGreater(Version newVersion, Version currentVersion) {
			try {
				var normalizedNew = new Version(newVersion.Major, newVersion.Minor, newVersion.Build >= 0 ? newVersion.Build : 0, newVersion.Revision >= 0 ? newVersion.Revision : 0);

				var normalizedCurrent = new Version(currentVersion.Major,
					currentVersion.Minor,
					currentVersion.Build >= 0 ? currentVersion.Build : 0,
					currentVersion.Revision >= 0 ? currentVersion.Revision : 0);

				return normalizedNew > normalizedCurrent;
			} catch (Exception ex) {
				_logger.LogError(ex, "Error comparing versions {NewVersion} and {CurrentVersion}", newVersion, currentVersion);
				return false;
			}
		}

		public async Task CheckForUpdatesOnStartupAsync() {
			if (IsCheckingForUpdates) {
				_logger.LogDebug("Update check already in progress, skipping startup check");
				return;
			}

			try {
				_logger.LogInformation("Starting update check on startup");

				// Load auto-update configuration
				var isAutoUpdateEnabled = await _configService.IsAutoUpdateEnabledAsync();
				AutoUpdateEnabled = isAutoUpdateEnabled;

				_logger.LogInformation("Auto-update enabled: {AutoUpdateEnabled}", AutoUpdateEnabled);

				await CheckForUpdatesAsync();

				if (await IsUpdateAvailableAsync()) {
					_logger.LogInformation("Update available: {CurrentVersion} -> {NewVersion}", _updateService.GetCurrentVersion(), AvailableUpdate?.Version);

					// If auto-update is enabled, automatically download and install
					if (AutoUpdateEnabled) {
						_logger.LogInformation("Starting automatic update process");
						await AutoUpdateAsync();
					} else {
						_logger.LogInformation("Auto-update disabled, manual update required");
					}
				} else {
					_logger.LogInformation("Application is up to date, version: {Version}", _updateService.GetCurrentVersion());
					StatusMessage = "Application is up to date - you have the latest version";
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Error during startup update check");
				StatusMessage = "Failed to check for updates on startup";
			}
		}

		private async Task AutoUpdateAsync() {
			if (!await IsUpdateAvailableAsync()) {
				_logger.LogInformation("No updates available for auto-update");
				StatusMessage = "Application is up to date - you have the latest version";
				return;
			}

			UpdateProgressWindow? progressWindow = null;
			UpdateProgressViewModel? progressViewModel = null;

			try {
				_logger.LogInformation("Starting auto-update process for version {Version}", AvailableUpdate?.Version);

				// Create and show progress window on UI thread
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel = new UpdateProgressViewModel();
					progressViewModel.SetVersions(_updateService.GetCurrentVersion().ToString(), AvailableUpdate?.Version?.ToString() ?? "Unknown");

					progressWindow = new UpdateProgressWindow {
						DataContext = progressViewModel
					};
					progressWindow.Show();
				});

				// Download update
				_logger.LogInformation("Starting download for version {Version}", AvailableUpdate?.Version);

				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(0, "Starting update download...");
				});

				var progressIndicator = new Progress<int>(percent => {
					Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(percent, $"Downloading update... {percent}%");
					});
				});

				string? filePath = AvailableUpdate != null ? await _updateService.DownloadUpdateAsync(AvailableUpdate, progressIndicator) : null;

				if (string.IsNullOrEmpty(filePath)) {
					_logger.LogWarning("Auto-update download failed");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(0, "Download failed");
					});
					await Task.Delay(2000);
					return;
				}

				// Install update
				_logger.LogInformation("Installing update from: {FilePath}", filePath);

				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(100, "Installing update...");
				});

				bool installResult = await _updateService.InstallUpdateAsync(filePath);

				if (installResult) {
					_logger.LogInformation("Auto-update installation successful");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(100, "Update installed. Restarting application...");
					});
					await Task.Delay(2000);
				} else {
					_logger.LogError("Auto-update installation failed");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(100, "Installation failed");
					});
					await Task.Delay(2000);
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Auto-update process failed");
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(0, $"Update error: {ex.Message}");
				});
				await Task.Delay(3000);
			} finally {
				// Close progress window
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressWindow?.Close();
				});
			}
		}

		public async Task CheckForUpdatesAsync() {
			if (IsCheckingForUpdates) {
				_logger.LogDebug("Update check already in progress");
				return;
			}

			IsCheckingForUpdates = true;
			StatusMessage = "Checking for updates...";
			DownloadProgress = 0;

			try {
				_logger.LogInformation("Checking for available updates");
				AvailableUpdate = await _updateService.CheckForUpdatesAsync();

				var currentVersion = _updateService.GetCurrentVersion();

				if (await IsUpdateAvailableAsync()) {
					StatusMessage = $"Update available: {AvailableUpdate?.Version}";
					_logger.LogInformation("Update found: {CurrentVersion} -> {NewVersion}", currentVersion, AvailableUpdate?.Version);
				} else {
					StatusMessage = "Application is up to date - you have the latest version";
					_logger.LogInformation("No updates available. Current version: {Version}", currentVersion);
					AvailableUpdate = null;
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Failed to check for updates");
				StatusMessage = $"Failed to check for updates: {ex.Message}";
				AvailableUpdate = null;
			} finally {
				IsCheckingForUpdates = false;
				await Dispatcher.UIThread.InvokeAsync(() => {
					DownloadAndUpdateCommand.NotifyCanExecuteChanged();
				});
			}
		}

		private async Task<bool> CanDownloadUpdateAndInstallAsync() {
			return await IsUpdateAvailableAsync() && !IsCheckingForUpdates && !IsDownloadingUpdate && !IsInstallingUpdate;
		}

		private async Task DownloadUpdateAndInstallAsync() {
			if (!await IsUpdateAvailableAsync()) {
				_logger.LogWarning("Download called but no update available");
				StatusMessage = "No update available";
				return;
			}

			if (string.IsNullOrEmpty(AvailableUpdate?.DownloadUrl)) {
				_logger.LogWarning("Download URL is missing");
				StatusMessage = "Update download URL is missing";
				return;
			}

			UpdateProgressWindow? progressWindow = null;
			UpdateProgressViewModel? progressViewModel = null;

			try {
				_logger.LogInformation("Starting manual update download for version {Version}", AvailableUpdate?.Version);

				// Create and show progress window on UI thread
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel = new UpdateProgressViewModel();
					progressViewModel.SetVersions(_updateService.GetCurrentVersion().ToString(), AvailableUpdate?.Version?.ToString() ?? "Unknown");

					progressWindow = new UpdateProgressWindow {
						DataContext = progressViewModel
					};
					progressWindow.Show();
				});

				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(0, "Starting update download...");
				});

				var progressIndicator = new Progress<int>(percent => {
					Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(percent, $"Downloading update... {percent}%");
					});
				});

				string? downloadedFilePath = AvailableUpdate != null ? await _updateService.DownloadUpdateAsync(AvailableUpdate, progressIndicator) : null;

				if (string.IsNullOrEmpty(downloadedFilePath)) {
					_logger.LogError("Update download failed - file path is empty");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(0, "Download failed");
					});
					await Task.Delay(2000);
					return;
				}

				_logger.LogInformation("Update downloaded successfully, starting installation");

				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(100, "Installing update...");
				});

				bool installResult = await _updateService.InstallUpdateAsync(downloadedFilePath);

				if (installResult) {
					_logger.LogInformation("Update installation successful");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(100, "Update installed. Restarting application...");
					});
					await Task.Delay(2000);
				} else {
					_logger.LogError("Update installation failed");
					await Dispatcher.UIThread.InvokeAsync(() => {
						progressViewModel?.UpdateProgress(100, "Installation failed");
					});
					await Task.Delay(2000);
				}
			} catch (Exception ex) {
				_logger.LogError(ex, "Error during manual update process");
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressViewModel?.UpdateProgress(0, $"Update error: {ex.Message}");
				});
				await Task.Delay(3000);
			} finally {
				await Dispatcher.UIThread.InvokeAsync(() => {
					progressWindow?.Close();
				});

				IsDownloadingUpdate = false;
				IsInstallingUpdate = false;
				await Dispatcher.UIThread.InvokeAsync(() => {
					DownloadAndUpdateCommand.NotifyCanExecuteChanged();
				});
			}
		}

		private async Task SetAutoUpdateEnabledAsync(bool enabled) {
			try {
				await _configService.SetAutoUpdateEnabledAsync(enabled);
				AutoUpdateEnabled = enabled;
				_logger.LogInformation("Auto-update {Status}", enabled ? "enabled" : "disabled");
			} catch (Exception ex) {
				_logger.LogError(ex, "Failed to update auto-update setting");
			}
		}
	}
}