using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LogParserApp.Models;
using Microsoft.Extensions.Logging;

namespace LogParserApp.Services
{
    /// <summary>
    /// Интерфейс сервиса рекомендаций по ошибкам
    /// </summary>
    public interface IErrorRecommendationService
    {
        /// <summary>
        /// Инициализировать сервис и загрузить рекомендации из файла
        /// </summary>
        Task InitializeAsync();
        
        /// <summary>
        /// Проанализировать сообщение об ошибке и получить рекомендации
        /// </summary>
        ErrorRecommendationResult? AnalyzeError(string errorMessage);
        
        /// <summary>
        /// Проверить наличие рекомендаций для конкретного типа ошибки
        /// </summary>
        bool HasRecommendationForErrorType(string errorType);
        
        /// <summary>
        /// Обновить рекомендации из указанного файла
        /// </summary>
        Task UpdateRecommendationsFromFileAsync(string filePath);
        
        /// <summary>
        /// Получить путь к активному файлу рекомендаций
        /// </summary>
        string GetActiveRecommendationsFilePath();
        
        /// <summary>
        /// Сохранить текущие рекомендации в новый файл
        /// </summary>
        Task<bool> SaveRecommendationsToFileAsync(string filePath);
        
        /// <summary>
        /// Загрузить рекомендации из файла пользователя по умолчанию
        /// </summary>
        Task<bool> LoadUserRecommendationsAsync();
        
        /// <summary>
        /// Сохранить рекомендации в файл пользователя по умолчанию
        /// </summary>
        Task<bool> SaveUserRecommendationsAsync();
    }
    
    /// <summary>
    /// Сервис для работы с рекомендациями по ошибкам
    /// </summary>
    public class ErrorRecommendationService : IErrorRecommendationService
    {
        private readonly ILogger<ErrorRecommendationService> _logger;
        private readonly List<ErrorRecommendation> _recommendations = new();
        private const string DefaultRecommendationFile = "error_recommendations.json";
        private bool _isInitialized = false;
        private string _activeFilePath = string.Empty;
        
        public ErrorRecommendationService(ILogger<ErrorRecommendationService> logger)
        {
            _logger = logger;
        }
        
        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogDebug("Service already initialized");
                return;
            }
                
            try
            {
                // Ищем файл в нескольких местах
                var filePath = FindRecommendationsFile();
                
                _logger.LogInformation("Attempting to load recommendations from path: {FilePath}", filePath);
                
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    _logger.LogWarning("Recommendations file not found. Creating default file.");
                    filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
                    await CreateEmptyRecommendationsFileAsync(filePath);
                }
                
                await LoadRecommendationsFromFileAsync(filePath);
                _activeFilePath = filePath;
                _isInitialized = true;
                
                _logger.LogInformation("Successfully loaded {Count} recommendations from {FilePath}", _recommendations.Count, filePath);
                
                // Логируем загруженные шаблоны
                foreach (var rec in _recommendations)
                {
                    _logger.LogDebug("Loaded pattern: {Pattern} for error type: {ErrorType}", rec.ErrorPattern, rec.ErrorType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing error recommendation service");
                // Создаем пустой список рекомендаций, чтобы сервис мог работать
                _recommendations.Clear();
                _isInitialized = true;
            }
        }
        
        /// <inheritdoc />
        public ErrorRecommendationResult? AnalyzeError(string errorMessage)
        {
            if (!_isInitialized)
            {
                _logger.LogWarning("Error recommendation service is not initialized");
                return null;
            }
            
            _logger.LogDebug("Analyzing error message: {Message}", errorMessage);
            _logger.LogDebug("Available patterns count: {Count}", _recommendations.Count);
            
            foreach (var recommendation in _recommendations)
            {
                _logger.LogDebug("Checking pattern: {Pattern}", recommendation.ErrorPattern);
                if (recommendation.IsMatch(errorMessage))
                {
                    _logger.LogDebug("Found matching pattern for error type: {ErrorType}", recommendation.ErrorType);
                    return recommendation.GetRecommendationResult(errorMessage);
                }
            }
            
            _logger.LogDebug("No matching pattern found for error message");
            return null;
        }
        
        /// <inheritdoc />
        public bool HasRecommendationForErrorType(string errorType)
        {
            return _recommendations.Exists(r => r.ErrorType.Equals(errorType, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <inheritdoc />
        public async Task UpdateRecommendationsFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Recommendations file {FilePath} not found", filePath);
                return;
            }
            
            await LoadRecommendationsFromFileAsync(filePath);
            _activeFilePath = filePath;
            _logger.LogInformation("Updated recommendations from file {FilePath}", filePath);
        }
        
        /// <inheritdoc />
        public string GetActiveRecommendationsFilePath()
        {
            return _activeFilePath;
        }
        
        /// <inheritdoc />
        public async Task<bool> SaveRecommendationsToFileAsync(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_recommendations, options);
                
                // Создаем директорию, если она не существует
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation("Saved recommendations to file {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recommendations to file {FilePath}", filePath);
                return false;
            }
        }
        
        /// <inheritdoc />
        public async Task<bool> LoadUserRecommendationsAsync()
        {
            string filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("User recommendations file not found at {FilePath}", filePath);
                return false;
            }
            
            try
            {
                await LoadRecommendationsFromFileAsync(filePath);
                _activeFilePath = filePath;
                _logger.LogInformation("Loaded user recommendations from {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user recommendations from {FilePath}", filePath);
                return false;
            }
        }
        
        /// <inheritdoc />
        public async Task<bool> SaveUserRecommendationsAsync()
        {
            string filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
            return await SaveRecommendationsToFileAsync(filePath);
        }
        
        /// <summary>
        /// Загрузить рекомендации из файла
        /// </summary>
        private async Task LoadRecommendationsFromFileAsync(string filePath)
        {
            try
            {
                _logger.LogInformation("Reading recommendations file: {FilePath}", filePath);
                string json = await File.ReadAllTextAsync(filePath);
                _logger.LogDebug("JSON content length: {Length}", json.Length);
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };
                
                var recommendations = JsonSerializer.Deserialize<List<ErrorRecommendation>>(json, options);
                
                if (recommendations != null)
                {
                    _recommendations.Clear();
                    _recommendations.AddRange(recommendations);
                    _logger.LogInformation("Loaded {Count} error recommendations", _recommendations.Count);
                    
                    // Логируем типы загруженных рекомендаций
                    foreach (var rec in _recommendations)
                    {
                        _logger.LogDebug("Loaded recommendation for error type: {ErrorType}", rec.ErrorType);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize recommendations from file");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recommendations from file {FilePath}", filePath);
                throw;
            }
        }
        
        /// <summary>
        /// Создать пустой файл с рекомендациями
        /// </summary>
        private async Task CreateEmptyRecommendationsFileAsync(string filePath)
        {
            try
            {
                var emptyRecommendations = new List<ErrorRecommendation>();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(emptyRecommendations, options);
                
                // Создаем директорию, если она не существует
                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                await File.WriteAllTextAsync(filePath, json);
                _logger.LogInformation("Created empty recommendations file at {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating empty recommendations file at {FilePath}", filePath);
            }
        }
        
        /// <summary>
        /// Найти файл рекомендаций в разных местах
        /// </summary>
        private string FindRecommendationsFile()
        {
            var possibleLocations = new[]
            {
                // В пользовательском каталоге
                Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile),
                
                // В текущей директории
                Path.Combine(Directory.GetCurrentDirectory(), DefaultRecommendationFile),
                
                // В директории сборки
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultRecommendationFile),
                
                // В директории на уровень выше от сборки
                Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty, DefaultRecommendationFile),
            };
            
            foreach (var location in possibleLocations)
            {
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                {
                    _logger.LogInformation("Found recommendations file at {FilePath}", location);
                    return location;
                }
            }
            
            // Если файл не найден во всех возможных местах
            _logger.LogWarning("Recommendations file not found in any of the expected locations");
            return string.Empty;
        }
        
        /// <summary>
        /// Получить директорию для хранения пользовательских рекомендаций
        /// </summary>
        private string GetUserRecommendationsDirectory()
        {
            // Создаем директорию в AppData или .config на Linux/Mac
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userRecommendationsDir = Path.Combine(appDataPath, "LogParserApp", "Recommendations");
            
            // Создаем директорию, если она не существует
            if (!Directory.Exists(userRecommendationsDir))
            {
                Directory.CreateDirectory(userRecommendationsDir);
            }
            
            return userRecommendationsDir;
        }
    }
} 