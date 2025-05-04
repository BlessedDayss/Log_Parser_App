namespace Log_Parser_App.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Microsoft.Extensions.Logging;



    public interface IErrorRecommendationService
    {
        Task InitializeAsync();

        ErrorRecommendationResult? AnalyzeError(string errorMessage);

        bool HasRecommendationForErrorType(string errorType);

        Task UpdateRecommendationsFromFileAsync(string filePath);

        string GetActiveRecommendationsFilePath();

        Task<bool> SaveRecommendationsToFileAsync(string filePath);

        Task<bool> LoadUserRecommendationsAsync();

        Task<bool> SaveUserRecommendationsAsync();
    }

    public class ErrorRecommendationService(ILogger<ErrorRecommendationService> logger) : IErrorRecommendationService
    {
        private readonly List<ErrorRecommendation> _recommendations = [];
        private const string DefaultRecommendationFile = "error_recommendations.json";
        private bool _isInitialized = false;
        private string _activeFilePath = string.Empty;

        public async Task InitializeAsync() {
            if (_isInitialized) {
                logger.LogDebug("Service already initialized");
                return;
            }

            try {
                var filePath = FindRecommendationsFile();

                logger.LogInformation("Attempting to load recommendations from path: {FilePath}", filePath);

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                    logger.LogWarning("Recommendations file not found. Creating default file.");
                    filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
                    await CreateEmptyRecommendationsFileAsync(filePath);
                }

                await LoadRecommendationsFromFileAsync(filePath);
                _activeFilePath = filePath;
                _isInitialized = true;

                logger.LogInformation("Successfully loaded {Count} recommendations from {FilePath}", _recommendations.Count, filePath);

                foreach (var rec in _recommendations) {
                    logger.LogDebug("Loaded pattern: {Pattern} for error type: {ErrorType}", rec.ErrorPattern, rec.ErrorType);
                }
            } catch (Exception ex) {
                logger.LogError(ex, "Error initializing error recommendation service");
                _recommendations.Clear();
                _isInitialized = true;
            }
        }

        public ErrorRecommendationResult? AnalyzeError(string errorMessage) {
            if (!_isInitialized) {
                logger.LogWarning("Error recommendation service is not initialized");
                return null;
            }

            logger.LogDebug("Analyzing error message: {Message}", errorMessage);
            logger.LogDebug("Available patterns count: {Count}", _recommendations.Count);

            foreach (var recommendation in _recommendations) {
                logger.LogDebug("Checking pattern: {Pattern}", recommendation.ErrorPattern);
                if (recommendation.IsMatch(errorMessage)) {
                    logger.LogDebug("Found matching pattern for error type: {ErrorType}", recommendation.ErrorType);
                    return recommendation.GetRecommendationResult(errorMessage);
                }
            }

            logger.LogDebug("No matching pattern found for error message");
            return null;
        }

        public bool HasRecommendationForErrorType(string errorType) {
            return _recommendations.Exists(r => r.ErrorType.Equals(errorType, StringComparison.OrdinalIgnoreCase));
        }


        public async Task UpdateRecommendationsFromFileAsync(string filePath) {
            if (!File.Exists(filePath)) {
                logger.LogWarning("Recommendations file {FilePath} not found", filePath);
                return;
            }

            await LoadRecommendationsFromFileAsync(filePath);
            _activeFilePath = filePath;
            logger.LogInformation("Updated recommendations from file {FilePath}", filePath);
        }

        public string GetActiveRecommendationsFilePath() {
            return _activeFilePath;
        }


        public async Task<bool> SaveRecommendationsToFileAsync(string filePath) {
            try {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_recommendations, options);

                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation("Saved recommendations to file {FilePath}", filePath);
                return true;
            } catch (Exception ex) {
                logger.LogError(ex, "Error saving recommendations to file {FilePath}", filePath);
                return false;
            }
        }


        public async Task<bool> LoadUserRecommendationsAsync() {
            string filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
            if (!File.Exists(filePath)) {
                logger.LogWarning("User recommendations file not found at {FilePath}", filePath);
                return false;
            }

            try {
                await LoadRecommendationsFromFileAsync(filePath);
                _activeFilePath = filePath;
                logger.LogInformation("Loaded user recommendations from {FilePath}", filePath);
                return true;
            } catch (Exception ex) {
                logger.LogError(ex, "Error loading user recommendations from {FilePath}", filePath);
                return false;
            }
        }


        public async Task<bool> SaveUserRecommendationsAsync() {
            string filePath = Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile);
            return await SaveRecommendationsToFileAsync(filePath);
        }

        private async Task LoadRecommendationsFromFileAsync(string filePath) {
            try {
                logger.LogInformation("Reading recommendations file: {FilePath}", filePath);
                string json = await File.ReadAllTextAsync(filePath);
                logger.LogDebug("JSON content length: {Length}", json.Length);

                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true
                };

                var recommendations = JsonSerializer.Deserialize<List<ErrorRecommendation>>(json, options);

                if (recommendations != null) {
                    _recommendations.Clear();
                    _recommendations.AddRange(recommendations);
                    logger.LogInformation("Loaded {Count} error recommendations", _recommendations.Count);

                    foreach (var rec in _recommendations) {
                        logger.LogDebug("Loaded recommendation for error type: {ErrorType}", rec.ErrorType);
                    }
                } else {
                    logger.LogWarning("Failed to deserialize recommendations from file");
                }
            } catch (Exception ex) {
                logger.LogError(ex, "Error loading recommendations from file {FilePath}", filePath);
                throw;
            }
        }

        private async Task CreateEmptyRecommendationsFileAsync(string filePath) {
            try {
                var emptyRecommendations = new List<ErrorRecommendation>();
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(emptyRecommendations, options);

                string? directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath)) {
                    Directory.CreateDirectory(directoryPath);
                }

                await File.WriteAllTextAsync(filePath, json);
                logger.LogInformation("Created empty recommendations file at {FilePath}", filePath);
            } catch (Exception ex) {
                logger.LogError(ex, "Error creating empty recommendations file at {FilePath}", filePath);
            }
        }


        private string FindRecommendationsFile() {
            string[] possibleLocations = new[] {
                Path.Combine(GetUserRecommendationsDirectory(), DefaultRecommendationFile),

                Path.Combine(Directory.GetCurrentDirectory(), DefaultRecommendationFile),

                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultRecommendationFile),

                Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)?.FullName ?? string.Empty, DefaultRecommendationFile),
            };

            foreach (string location in possibleLocations) {
                if (!string.IsNullOrEmpty(location) && File.Exists(location)) {
                    logger.LogInformation("Found recommendations file at {FilePath}", location);
                    return location;
                }
            }

            logger.LogWarning("Recommendations file not found in any of the expected locations");
            return string.Empty;
        }

        private static string GetUserRecommendationsDirectory() {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userRecommendationsDir = Path.Combine(appDataPath, "LogParserApp", "Recommendations");

            if (!Directory.Exists(userRecommendationsDir)) {
                Directory.CreateDirectory(userRecommendationsDir);
            }

            return userRecommendationsDir;
        }
    }
}