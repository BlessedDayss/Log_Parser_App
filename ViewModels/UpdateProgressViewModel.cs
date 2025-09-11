using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Log_Parser_App.ViewModels
{
    public partial class UpdateProgressViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string _updateMessage = "Preparing for update...";

        [ObservableProperty]
        private string _currentVersion = string.Empty;

        [ObservableProperty]
        private string _newVersion = string.Empty;

        [ObservableProperty]
        private string _progressText = "Initializing...";

        [ObservableProperty]
        private int _progressValue = 0;

        [ObservableProperty]
        private int _progressPercentage = 0;

        [ObservableProperty]
        private string _statusMessage = "Preparing application update";

        public void UpdateProgress(int percentage, string message)
        {
            ProgressValue = percentage;
            ProgressPercentage = percentage;
            ProgressText = message;
            StatusMessage = message;
        }

        public void SetVersions(string current, string newVersion)
        {
            CurrentVersion = current;
            NewVersion = newVersion;
            UpdateMessage = $"New version {newVersion} available";
        }
    }
} 