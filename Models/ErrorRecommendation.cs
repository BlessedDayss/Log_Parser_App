using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace LogParserApp.Models
{
    /// <summary>
    /// Модель для хранения информации о рекомендациях по ошибкам
    /// </summary>
    public class ErrorRecommendation
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ErrorRecommendation>();
        
        /// <summary>
        /// Регулярное выражение для поиска ошибки в логе
        /// </summary>
        [JsonPropertyName("error_pattern")]
        public string ErrorPattern { get; set; } = string.Empty;
        
        /// <summary>
        /// Уникальный идентификатор типа ошибки
        /// </summary>
        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Описание ошибки с возможностью подстановки переменных
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Список рекомендаций по исправлению ошибки
        /// </summary>
        [JsonPropertyName("recommendations")]
        public List<string> Recommendations { get; set; } = new List<string>();
        
        /// <summary>
        /// Скомпилированное регулярное выражение для быстрого поиска
        /// </summary>
        [JsonIgnore]
        public Regex? CompiledPattern { get; set; }
        
        /// <summary>
        /// Проверить, соответствует ли сообщение об ошибке шаблону
        /// </summary>
        public bool IsMatch(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                Logger.LogDebug("Empty error message");
                return false;
            }

            if (CompiledPattern == null && !string.IsNullOrEmpty(ErrorPattern))
            {
                Logger.LogDebug("Compiling pattern: {Pattern}", ErrorPattern);
                try
                {
                    CompiledPattern = new Regex(ErrorPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error compiling pattern: {Pattern}", ErrorPattern);
                    return false;
                }
            }
            
            if (CompiledPattern == null)
            {
                Logger.LogWarning("No pattern available for matching");
                return false;
            }
            
            // Логирование для отладки
            Logger.LogDebug("Checking pattern '{Pattern}' against message: '{Message}'", ErrorPattern, errorMessage);
            
            bool isMatch = CompiledPattern.IsMatch(errorMessage);
            Logger.LogDebug("Match result for {ErrorType}: {IsMatch}", ErrorType, isMatch);
            
            return isMatch;
        }
        
        /// <summary>
        /// Получить рекомендации с подстановкой переменных
        /// </summary>
        public ErrorRecommendationResult GetRecommendationResult(string errorMessage)
        {
            var result = new ErrorRecommendationResult
            {
                ErrorType = ErrorType,
                Description = Description,
                Recommendations = new List<string>(Recommendations)
            };
            
            if (CompiledPattern == null && !string.IsNullOrEmpty(ErrorPattern))
            {
                CompiledPattern = new Regex(ErrorPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            
            if (CompiledPattern != null)
            {
                var match = CompiledPattern.Match(errorMessage);
                if (match.Success)
                {
                    // Извлекаем переменные из сообщения об ошибке
                    var variables = new Dictionary<string, string>();
                    
                    // Если есть группа для имени пакета
                    if (match.Groups.Count > 1)
                    {
                        variables["package_name"] = match.Groups[1].Value;
                        Logger.LogDebug("Extracted package name: {PackageName}", match.Groups[1].Value);
                    }
                    
                    // Добавляем детали ошибки
                    variables["error_details"] = errorMessage;
                    
                    // Заменяем переменные в описании
                    result.Description = ReplaceVariables(Description, variables);
                    
                    // Заменяем переменные в рекомендациях
                    for (int i = 0; i < result.Recommendations.Count; i++)
                    {
                        result.Recommendations[i] = ReplaceVariables(result.Recommendations[i], variables);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Заменить переменные в шаблоне
        /// </summary>
        private string ReplaceVariables(string template, Dictionary<string, string> variables)
        {
            string result = template;
            foreach (var variable in variables)
            {
                result = result.Replace($"{{{variable.Key}}}", variable.Value);
            }
            return result;
        }
    }
    
    /// <summary>
    /// Результат с подготовленными рекомендациями
    /// </summary>
    public class ErrorRecommendationResult
    {
        /// <summary>
        /// Тип ошибки
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;
        
        /// <summary>
        /// Описание ошибки с подставленными переменными
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Список рекомендаций с подставленными переменными
        /// </summary>
        public List<string> Recommendations { get; set; } = new List<string>();
    }
} 