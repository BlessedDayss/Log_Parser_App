using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Log_Parser_App.Services
{
    public static class GitHubTokenService
    {
        // Расширенные форматы токенов GitHub
        private static readonly string[] TokenPatterns = {
            @"^ghp_[a-zA-Z0-9]{36}$",     // Старый формат Personal Access Token
            @"^github_pat_[a-zA-Z0-9_]+$" // Новый формат Fine-grained Personal Access Token
        };

        public static string GetGitHubToken()
        {
            // Получение токена из переменных окружения
            string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? string.Empty;
            
            // Проверка токена по различным форматам
            if (IsValidGitHubToken(token))
            {
                return token;
            }

            // Возвращаем пустую строку, если токен не прошел валидацию
            return string.Empty;
        }

        private static bool IsValidGitHubToken(string token)
        {
            // Проверка токена на соответствие различным форматам
            return !string.IsNullOrWhiteSpace(token) && 
                   TokenPatterns.Any(pattern => Regex.IsMatch(token, pattern));
        }
    }
}
