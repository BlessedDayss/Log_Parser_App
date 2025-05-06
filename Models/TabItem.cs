using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;

namespace Log_Parser_App.Models;

public partial class TabItem : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private ObservableCollection<LogEntry> _logEntries = new();

    [ObservableProperty]
    private ObservableCollection<LogEntry> _filteredLogEntries = new();

    [ObservableProperty]
    private ObservableCollection<LogEntry> _errorLogEntries = new();

    [ObservableProperty]
    private LogEntry? _selectedLogEntry;

    [ObservableProperty]
    private ISeries[] _levelsOverTimeSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _topErrorsSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _logDistributionSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _timeHeatmapSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _errorTrendSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private ISeries[] _sourcesDistributionSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private LogStatistics _tabStatistics = new();

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private int _infoCount;

    [ObservableProperty]
    private int _otherCount;

    [ObservableProperty]
    private double _errorPercent;

    [ObservableProperty]
    private double _warningPercent;

    [ObservableProperty]
    private double _infoPercent;

    [ObservableProperty]
    private double _otherPercent;
}