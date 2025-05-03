using System;
using System.Collections.Generic;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Класс для хранения информации о доступном обновлении
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// Версия обновления
        /// </summary>
        public Version Version { get; set; } = new Version();
        
        /// <summary>
        /// Название релиза
        /// </summary>
        public string ReleaseName { get; set; } = string.Empty;
        
        /// <summary>
        /// Описание релиза
        /// </summary>
        public string ReleaseNotes { get; set; } = string.Empty;
        
        /// <summary>
        /// URL для загрузки файла обновления
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Размер файла обновления в байтах
        /// </summary>
        public long FileSize { get; set; }
        
        /// <summary>
        /// Дата публикации обновления
        /// </summary>
        public DateTime PublishedAt { get; set; }
        
        /// <summary>
        /// Список изменений в релизе
        /// </summary>
        public List<string> ChangeLog { get; set; } = new List<string>();
        
        /// <summary>
        /// Требуется ли перезапуск приложения после обновления
        /// </summary>
        public bool RequiresRestart { get; set; } = true;
        
        /// <summary>
        /// Тег релиза в GitHub (например, "v0.1.5")
        /// </summary>
        public string TagName { get; set; } = string.Empty;
    }
} 