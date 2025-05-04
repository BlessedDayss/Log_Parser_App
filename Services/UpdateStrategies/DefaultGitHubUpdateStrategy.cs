namespace Log_Parser_App.Services.UpdateStrategies
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Log_Parser_App.Models;
    using Log_Parser_App.Services;
    using Log_Parser_App.Services.Interfaces;
    using Microsoft.Extensions.Logging;

    public class DefaultGitHubUpdateStrategy : IGitHubUpdateStrategy
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DefaultGitHubUpdateStrategy> _logger;
        private readonly IVersionParser _versionParser;
        private readonly string _owner;
        private readonly string _repo;

        public DefaultGitHubUpdateStrategy(
            HttpClient httpClient, 
            ILogger<DefaultGitHubUpdateStrategy> logger, 
            IVersionParser versionParser,
            string owner,
            string repo)
        {
            _httpClient = httpClient;
            _logger = logger;
            _versionParser = versionParser;
            _owner = owner;
            _repo = repo;
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync(GitHubConnectionResult connectionResult)
        {
            if (!connectionResult.Success)
            {
                _logger.LogWarning("GitHub connection test failed: {Message}", connectionResult.Message);
                return null;
            }

            var currentVersion = GetCurrentVersion();
            var latestVersion = _versionParser.ParseVersion(connectionResult.TagName ?? string.Empty);

            if (latestVersion == null) return null;

            bool updateAvailable = latestVersion > currentVersion;
            LogVersionComparison(updateAvailable, currentVersion, latestVersion);

            if (!updateAvailable) return null;

            return await FetchUpdateDetailsAsync(connectionResult, latestVersion);
        }

        private Version GetCurrentVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version 
                   ?? new Version(1, 0, 0);
        }

        private void LogVersionComparison(bool updateAvailable, Version currentVersion, Version latestVersion)
        {
            _logger.LogInformation("Latest version: {Version}", latestVersion);
            _logger.LogInformation("Update available: {Available} (Current: {Current}, Latest: {Latest})", 
                updateAvailable, currentVersion, latestVersion);
        }

        private async Task<UpdateInfo?> FetchUpdateDetailsAsync(GitHubConnectionResult testResult, Version latestVersion)
        {
            string releaseUrl = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
            
            var request = new HttpRequestMessage(HttpMethod.Get, releaseUrl);
            request.Headers.UserAgent.ParseAdd("Log_Parser_App/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github.v3+json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to get detailed release info: {StatusCode}, Error: {ErrorContent}", 
                    response.StatusCode, errorContent);
                return null;
            }

            string content = await response.Content.ReadAsStringAsync();
            var releaseInfo = System.Text.Json.JsonSerializer.Deserialize<GitHubReleaseInfo>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (releaseInfo == null)
            {
                _logger.LogWarning("Failed to parse GitHub release info");
                return null;
            }

            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseName = releaseInfo.Name ?? string.Empty,
                ReleaseNotes = releaseInfo.Body ?? string.Empty,
                DownloadUrl = testResult.DownloadUrl ?? string.Empty,
                TagName = testResult.TagName ?? string.Empty,
                PublishedAt = releaseInfo.PublishedAt ?? DateTime.Now,
                RequiresRestart = true
            };
        }
    }
}
