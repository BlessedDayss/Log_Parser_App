using System;
using System.Threading.Tasks;
using Log_Parser_App.Models;

namespace Log_Parser_App.Services
{

    public interface IUpdateService
    {
        Task<UpdateInfo> CheckForUpdatesAsync();
        
        Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progressCallback = null);
        
        Task<bool> InstallUpdateAsync(string updateFilePath);
        
        Version GetCurrentVersion();
    }
} 