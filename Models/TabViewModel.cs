using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Log_Parser_App.Models;
using System.IO;

namespace Log_Parser_App.Models
{
    public class TabViewModel : ObservableObject
    {
        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private List<LogEntry> _logEntries;
        public List<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TabViewModel(string filePath, string title, List<LogEntry> logEntries)
        {
            _filePath = filePath;
            _title = title;
            _logEntries = logEntries;
            _isSelected = false;
        }
    }
} 