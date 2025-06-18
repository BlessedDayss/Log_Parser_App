using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Log_Parser_App.Models;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services
{
    public interface ISimpleErrorRecommendationService
    {
        Task LoadAsync();
        SimpleErrorResult? AnalyzeError(string errorText);
    }

    public class SimpleErrorRecommendationService : ISimpleErrorRecommendationService
    {
        private readonly ILogger<SimpleErrorRecommendationService> _logger;
        private readonly List<SimpleErrorPattern> _patterns = new();
        private const string RecommendationsFile = "simple_error_recommendations.json";
        private bool _isLoaded = false;

        public SimpleErrorRecommendationService(ILogger<SimpleErrorRecommendationService> logger)
        {
            _logger = logger;
        }

        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(RecommendationsFile))
                {
                    _logger.LogWarning("Recommendations file not found: {File}", RecommendationsFile);
                    return;
                }

                string json = await File.ReadAllTextAsync(RecommendationsFile);
                var patterns = JsonSerializer.Deserialize<List<SimpleErrorPattern>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (patterns != null)
                {
                    _patterns.Clear();
                    _patterns.AddRange(patterns);
                    _isLoaded = true;
                    _logger.LogInformation("Loaded {Count} error patterns", _patterns.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load error recommendations");
            }
        }

        public SimpleErrorResult? AnalyzeError(string errorText)
        {
            if (string.IsNullOrWhiteSpace(errorText))
                return null;

            // Force load if not already loaded
            if (!_isLoaded)
            {
                LoadAsync().GetAwaiter().GetResult();
            }

            foreach (var pattern in _patterns)
            {
                var result = pattern.GetResult(errorText);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
} 