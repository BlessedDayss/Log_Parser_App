using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services;

public class LocalizationService : ILocalizationService
{
    private readonly ILogger<LocalizationService> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private CultureInfo _currentCulture;
        
    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<CultureInfo> AvailableCultures { get; }
    
    public CultureInfo CurrentCulture 
    {
        get => _currentCulture;
        private set
        {
            if (_currentCulture != value)
            {
                _currentCulture = value;
                OnPropertyChanged();
                OnPropertyChanged(string.Empty);
            }
        }
    }
        
    public LocalizationService(ILogger<LocalizationService> logger)
    {
        _logger = logger;
            
        // Define available cultures
        AvailableCultures = new List<CultureInfo>
        {
            new CultureInfo("ru-RU"), 
            new CultureInfo("uk-UA"), 
            new CultureInfo("en-US")  
        };
        
        _currentCulture = new CultureInfo("ru-RU");
        
        InitializeTranslations();
    }

    public void ChangeCulture(CultureInfo culture)
    {
        if (culture == null)
            throw new ArgumentNullException(nameof(culture));
                
        var cultureName = culture.Name;
            
        bool isSupported = false;
        foreach (var availableCulture in AvailableCultures)
        {
            if (availableCulture.Name == cultureName)
            {
                isSupported = true;
                break;
            }
        }
            
        if (!isSupported)
        {
            _logger.LogWarning("Attempted to set unsupported culture: {Culture}", cultureName);
            return;
        }
            
        _logger.LogInformation("Changing culture to: {Culture}", cultureName);
        CurrentCulture = culture;
    }
    
    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;
                
        var cultureName = CurrentCulture.Name;
            
        if (_translations.TryGetValue(cultureName, out var cultureTranslations))
        {
            if (cultureTranslations.TryGetValue(key, out var translation))
            {
                return translation;
            }
        }
            
        if (cultureName != "en-US" && _translations.TryGetValue("en-US", out var enTranslations))
        {
            if (enTranslations.TryGetValue(key, out var enTranslation))
            {
                return enTranslation;
            }
        }
            
        _logger.LogWarning("Translation not found for key: {Key} in culture: {Culture}", key, cultureName);
        return key;
    }
    
    private void InitializeTranslations()
    {
        try
        {
            _translations["ru-RU"] = new Dictionary<string, string>
            {
                // Window title
                { "AppTitle", "LogParser - Анализатор логов" },
                    
                // Top buttons
                { "LoadFile", "Загрузить файл" },
                { "FilterErrors", "Фильтровать ошибки" },
                { "ToggleDashboard", "Показать/скрыть статистику" },
                { "PackageLogs", "Логи пакетов" },
                { "ExportToCSV", "Экспорт в CSV" },
                    
                // Dashboard
                { "ERRORS", "ОШИБКИ" },
                { "WARNINGS", "ПРЕДУПРЕЖДЕНИЯ" },
                { "INFORMATION", "ИНФОРМАЦИЯ" },
                { "OTHERS", "ДРУГИЕ" },
                    
                // Query
                { "QueryWatermark", "Введите SQL запрос (например, SELECT * WHERE Level = 'Error')" },
                { "Execute", "Выполнить" },
                    
                // Tabs
                { "AllEntries", "Все записи" },
                { "QueryResults", "Результаты запроса" },
                { "ErrorsOnly", "Только ошибки" },
                { "PackageLogsTab", "Логи пакетов" },
                    
                // Table headers
                { "Time", "Время" },
                { "Level", "Уровень" },
                { "Source", "Источник" },
                { "Message", "Сообщение" },
                { "Recommendations", "Рекомендации" },
                { "PackageId", "ID пакета" },
                { "Version", "Версия" },
                { "Operation", "Операция" },
                    
                // Status bar
                { "TotalEntries", "Всего записей: {0}" },
                { "Ready", "Готов к работе" },
                    
                // Default error messages
                { "UnknownError", "Неизвестная ошибка. Рекомендации не найдены." },
                { "CheckErrorLog", "Проверьте лог ошибки для получения дополнительной информации." },
                { "ContactSupport", "Обратитесь к документации или службе поддержки." },
                    
                // Language selector
                { "Language", "Язык" },
                { "Russian", "Русский" },
                { "Ukrainian", "Украинский" },
                { "English", "Английский" }
            };
                
            // Ukrainian translations
            _translations["uk-UA"] = new Dictionary<string, string>
            {
                // Window title
                { "AppTitle", "LogParser - Аналізатор логів" },
                    
                // Top buttons
                { "LoadFile", "Завантажити файл" },
                { "FilterErrors", "Фільтрувати помилки" },
                { "ToggleDashboard", "Показати/сховати статистику" },
                { "PackageLogs", "Логи пакетів" },
                { "ExportToCSV", "Експорт в CSV" },
                    
                // Dashboard
                { "ERRORS", "ПОМИЛКИ" },
                { "WARNINGS", "ПОПЕРЕДЖЕННЯ" },
                { "INFORMATION", "ІНФОРМАЦІЯ" },
                { "OTHERS", "ІНШЕ" },
                    
                // Query
                { "QueryWatermark", "Введіть SQL запит (наприклад, SELECT * WHERE Level = 'Error')" },
                { "Execute", "Виконати" },
                    
                // Tabs
                { "AllEntries", "Всі записи" },
                { "QueryResults", "Результати запиту" },
                { "ErrorsOnly", "Тільки помилки" },
                { "PackageLogsTab", "Логи пакетів" },
                    
                // Table headers
                { "Time", "Час" },
                { "Level", "Рівень" },
                { "Source", "Джерело" },
                { "Message", "Повідомлення" },
                { "Recommendations", "Рекомендації" },
                { "PackageId", "ID пакету" },
                { "Version", "Версія" },
                { "Operation", "Операція" },
                    
                // Status bar
                { "TotalEntries", "Всього записів: {0}" },
                { "Ready", "Готовий до роботи" },
                    
                // Default error messages
                { "UnknownError", "Невідома помилка. Рекомендації не знайдені." },
                { "CheckErrorLog", "Перевірте лог помилки для отримання додаткової інформації." },
                { "ContactSupport", "Зверніться до документації або служби підтримки." },
                    
                // Language selector
                { "Language", "Мова" },
                { "Russian", "Російська" },
                { "Ukrainian", "Українська" },
                { "English", "Англійська" }
            };
                
            // English translations
            _translations["en-US"] = new Dictionary<string, string>
            {
                // Window title
                { "AppTitle", "LogParser - Log Analyzer" },
                    
                // Top buttons
                { "LoadFile", "Load file" },
                { "FilterErrors", "Filter errors" },
                { "ToggleDashboard", "Show/hide statistics" },
                { "PackageLogs", "Package Logs" },
                { "ExportToCSV", "Export to CSV" },
                    
                // Dashboard
                { "ERRORS", "ERRORS" },
                { "WARNINGS", "WARNINGS" },
                { "INFORMATION", "INFORMATION" },
                { "OTHERS", "OTHERS" },
                    
                // Query
                { "QueryWatermark", "Enter SQL query (for example, SELECT * WHERE Level = 'Error')" },
                { "Execute", "Execute" },
                    
                // Tabs
                { "AllEntries", "All entries" },
                { "QueryResults", "Query results" },
                { "ErrorsOnly", "Errors only" },
                { "PackageLogsTab", "Package Logs" },
                    
                // Table headers
                { "Time", "Time" },
                { "Level", "Level" },
                { "Source", "Source" },
                { "Message", "Message" },
                { "Recommendations", "Recommendations" },
                { "PackageId", "Package ID" },
                { "Version", "Version" },
                { "Operation", "Operation" },
                    
                // Status bar
                { "TotalEntries", "Total entries: {0}" },
                { "Ready", "Ready to work" },
                    
                // Default error messages
                { "UnknownError", "Unknown error. Recommendations not found." },
                { "CheckErrorLog", "Check the error log for additional information." },
                { "ContactSupport", "Contact documentation or support service." },
                    
                // Language selector
                { "Language", "Language" },
                { "Russian", "Russian" },
                { "Ukrainian", "Ukrainian" },
                { "English", "English" }
            };
                
            _logger.LogInformation("Initialized translations for {Count} languages", _translations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize translations");
        }
    }
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}