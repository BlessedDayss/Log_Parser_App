using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
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
        private string _statusMessage = "Готово к проверке обновлений";
        
        [ObservableProperty]
        private UpdateInfo? _availableUpdate;
        
        public UpdateViewModel(IUpdateService updateService, ILogger<UpdateViewModel> logger)
        {
            _updateService = updateService;
            _logger = logger;
        }

        private bool IsUpdateValid()
        {
            return AvailableUpdate != null && AvailableUpdate.Version != null && AvailableUpdate.Version > new Version(0, 0, 0);
        }
        
        public async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                _logger.LogInformation("Checking for updates on startup");
                
                await CheckForUpdatesAsync();
                
                if (IsUpdateValid())
                {
                    _logger.LogInformation("Update available: {Version}", AvailableUpdate.Version);
                }
                else
                {
                    // Ensure we don't keep an old update object around
                    AvailableUpdate = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates on startup");
            }
        }
        
        public async Task AutoUpdateAsync()
        {
            try
            {
                _logger.LogInformation("Starting auto update process");
                
                await CheckForUpdatesAsync();
                if (AvailableUpdate == null)
                {
                    _logger.LogInformation("No updates available");
                    return;
                }
                
                var filePath = await DownloadUpdateAsync();
                if (string.IsNullOrEmpty(filePath))
                {
                    _logger.LogWarning("Download failed or was cancelled");
                    return;
                }
                
                await InstallUpdateAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto update failed");
                StatusMessage = $"Ошибка: {ex.Message}";
            }
        }
        
        [RelayCommand]
        private async Task CheckForUpdates()
        {
            await CheckForUpdatesAsync();
        }
        
        private async Task CheckForUpdatesAsync()
        {
            if (IsCheckingForUpdates)
                return;
            
            IsCheckingForUpdates = true;
            StatusMessage = "Проверка обновлений...";
            
            try
            {
                AvailableUpdate = await _updateService.CheckForUpdatesAsync();
                
                if (AvailableUpdate != null && AvailableUpdate.Version != null && AvailableUpdate.Version != new Version(0, 0, 0))
                {
                    StatusMessage = $"Доступно обновление: {AvailableUpdate.Version}";
                }
                else
                {
                    StatusMessage = "У вас установлена последняя версия";
                    // Ensure we don't keep an old update object around
                    AvailableUpdate = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                StatusMessage = $"Ошибка проверки обновлений: {ex.Message}";
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }
        
        [RelayCommand]
        private async Task DownloadUpdate()
        {
            await DownloadUpdateAsync();
        }
        
        private async Task<string> DownloadUpdateAsync()
        {
            if (IsDownloadingUpdate || AvailableUpdate == null)
                return string.Empty;
            
            IsDownloadingUpdate = true;
            DownloadProgress = 0;
            StatusMessage = "Загрузка обновления...";
            
            try
            {
                var progress = new Progress<int>(percent =>
                {
                    DownloadProgress = percent;
                    StatusMessage = $"Загрузка обновления... {percent}%";
                });
                
                var filePath = await _updateService.DownloadUpdateAsync(AvailableUpdate, progress);
                StatusMessage = "Загрузка завершена";
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download update");
                StatusMessage = $"Ошибка загрузки: {ex.Message}";
                return string.Empty;
            }
            finally
            {
                IsDownloadingUpdate = false;
            }
        }
        
        [RelayCommand]
        private async Task InstallUpdate()
        {
            if (AvailableUpdate == null)
                return;
            
            var filePath = await DownloadUpdateAsync();
            if (string.IsNullOrEmpty(filePath))
                return;
            
            await InstallUpdateAsync(filePath);
        }
        
        private async Task InstallUpdateAsync(string filePath)
        {
            if (IsInstallingUpdate)
                return;
            
            IsInstallingUpdate = true;
            StatusMessage = "Установка обновления...";
            
            try
            {
                var result = await _updateService.InstallUpdateAsync(filePath);

                StatusMessage = result ? "Обновление успешно установлено. Требуется перезапуск приложения." : "Не удалось установить обновление";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install update");
                StatusMessage = $"Ошибка установки: {ex.Message}";
            }
            finally
            {
                IsInstallingUpdate = false;
            }
        }
    }
} 