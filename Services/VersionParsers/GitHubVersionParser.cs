using System;
using System.Text.RegularExpressions;
using Log_Parser_App.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.Services.VersionParsers;

public class GitHubVersionParser : IVersionParser
{
    private readonly ILogger<GitHubVersionParser> _logger;

    public GitHubVersionParser(ILogger<GitHubVersionParser> logger)
    {
        _logger = logger;
    }

    public Version? ParseVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            _logger.LogWarning("Empty version string provided");
            return null;
        }

        versionString = versionString.TrimStart('v');
        var versionMatch = Regex.Match(versionString, @"^(\d+\.\d+\.\d+)");

        if (!versionMatch.Success)
        {
            _logger.LogWarning("Could not extract version from string: {VersionString}", versionString);
            return null;
        }

        if (!Version.TryParse(versionMatch.Groups[1].Value, out var version))
        {
            _logger.LogWarning("Failed to parse version: {VersionString}", versionMatch.Groups[1].Value);
            return null;
        }

        return version;
    }
}
