namespace Log_Parser_App.Models
{
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;


    public abstract class ErrorRecommendation
    {
        private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<ErrorRecommendation>();
        
        [JsonPropertyName("error_pattern")]
        public string ErrorPattern { get; set; } = string.Empty;
        
        [JsonPropertyName("error_type")]
        public string ErrorType { get; set; } = string.Empty;
        
        [JsonPropertyName("description")] private string Description { get; set; } = string.Empty;
        
        [JsonPropertyName("recommendations")] private List<string> Recommendations { get; set; } = new List<string>();
        
        [JsonIgnore] private Regex? CompiledPattern { get; set; }
        
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
            

            Logger.LogDebug("Checking pattern '{Pattern}' against message: '{Message}'", ErrorPattern, errorMessage);
            
            bool isMatch = CompiledPattern.IsMatch(errorMessage);
            Logger.LogDebug("Match result for {ErrorType}: {IsMatch}", ErrorType, isMatch);
            
            return isMatch;
        }
        
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
                    var variables = new Dictionary<string, string>();
                    
                    if (match.Groups.Count > 1)
                    {
                        variables["package_name"] = match.Groups[1].Value;
                        Logger.LogDebug("Extracted package name: {PackageName}", match.Groups[1].Value);
                    }
                    
                    variables["error_details"] = errorMessage;
                    
                    result.Description = ReplaceVariables(Description, variables);
                    
                    for (int i = 0; i < result.Recommendations.Count; i++)
                    {
                        result.Recommendations[i] = ReplaceVariables(result.Recommendations[i], variables);
                    }
                }
            }
            
            return result;
        }
        

        private string ReplaceVariables(string template, Dictionary<string, string> variables)
        {
            return variables.Aggregate(template, (current, variable) => current.Replace($"{{{variable.Key}}}", variable.Value));
        }
    }
    
    public class ErrorRecommendationResult
    {

        public string ErrorType { get; init; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public List<string> Recommendations { get; init; } = [];
    }
} 