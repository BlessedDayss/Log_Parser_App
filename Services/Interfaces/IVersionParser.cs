using System;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services.Interfaces
{
    public interface IVersionParser
    {
        Version? ParseVersion(string versionString);
    }

    public interface IGitHubUpdateStrategy
    {
        Task<UpdateInfo?> CheckForUpdatesAsync(GitHubConnectionResult connectionResult);
    }

    public interface IGitHubConnectionService
    {
        Task<GitHubConnectionResult> TestConnectionAsync();
    }
}
