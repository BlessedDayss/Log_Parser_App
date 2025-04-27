using System;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services
{
    /// <summary>
    /// Интерфейс для сервиса автоматического обновления приложения
    /// </summary>
    public interface IUpdateService
    {
        /// <summary>
        /// Проверяет наличие новых обновлений
        /// </summary>
        /// <returns>Информация о доступном обновлении или null, если обновлений нет</returns>
        Task<UpdateInfo> CheckForUpdatesAsync();
        
        /// <summary>
        /// Загружает обновление
        /// </summary>
        /// <param name="updateInfo">Информация об обновлении</param>
        /// <param name="progressCallback">Callback для отображения прогресса загрузки</param>
        /// <returns>Путь к загруженному файлу обновления</returns>
        Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null);
        
        /// <summary>
        /// Устанавливает загруженное обновление
        /// </summary>
        /// <param name="updateFilePath">Путь к файлу обновления</param>
        /// <returns>true, если обновление успешно установлено</returns>
        Task<bool> InstallUpdateAsync(string updateFilePath);
        
        /// <summary>
        /// Получает текущую версию приложения
        /// </summary>
        /// <returns>Текущая версия приложения</returns>
        Version GetCurrentVersion();
    }
} 