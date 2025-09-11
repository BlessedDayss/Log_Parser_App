using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Log_Parser_App.Models
{
    public class SimpleErrorPattern
    {
        [JsonPropertyName("contains")]
        public string Contains { get; set; } = string.Empty;

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("fix")]
        public string Fix { get; set; } = string.Empty;

        public bool Matches(string errorText)
        {
            return errorText.Contains(Contains, StringComparison.OrdinalIgnoreCase);
        }

        public SimpleErrorResult? GetResult(string errorText)
        {
            if (!Matches(errorText))
                return null;

            string message = Message;
            string fix = Fix;

            if (!string.IsNullOrEmpty(Extract))
            {
                var regex = new Regex(Extract, RegexOptions.IgnoreCase);
                var match = regex.Match(errorText);
                
                if (match.Success)
                {
                    for (int i = 1; i < match.Groups.Count; i++)
                    {
                        string placeholder = $"{{{i}}}";
                        string value = match.Groups[i].Value;
                        message = message.Replace(placeholder, value);
                        fix = fix.Replace(placeholder, value);
                    }
                }
            }

            return new SimpleErrorResult
            {
                Message = message,
                Fix = fix
            };
        }
    }

    public class SimpleErrorResult
    {
        public string Message { get; set; } = string.Empty;
        public string Fix { get; set; } = string.Empty;
    }
} 