using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.Logging;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    /// ViewModel responsible for statistics and charts operations
    /// Follows Single Responsibility Principle
    /// </summary>
    public partial class StatisticsViewModel : ViewModelBase
    {
        #region Dependencies

        private readonly IChartService _chartService;
        private readonly ILogger<StatisticsViewModel> _logger;

        #endregion

        #region Properties

        [ObservableProperty]
        private LogStatistics _logStatistics = new();

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

        // Chart properties
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

        // Axis properties
        [ObservableProperty]
        private Axis[] _timeAxis = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _countAxis = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _daysAxis = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _hoursAxis = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _sourceAxis = Array.Empty<Axis>();

        [ObservableProperty]
        private Axis[] _errorMessageAxis = Array.Empty<Axis>();

        #endregion

        #region Events

        public event EventHandler<StatisticsUpdatedEventArgs>? StatisticsUpdated;

        #endregion

        #region Constructor

        public StatisticsViewModel(
            IChartService chartService,
            ILogger<StatisticsViewModel> logger)
        {
            _chartService = chartService;
            _logger = logger;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update statistics and charts based on log entries
        /// </summary>
        public void UpdateStatistics(IEnumerable<LogEntry> logEntries)
        {
            try
            {
                var entries = logEntries.ToList();
                _logger.LogDebug("Updating statistics for {Count} log entries", entries.Count);

                var result = CalculateStatisticsAndCharts(entries);

                // Update statistics
                ErrorCount = result.ErrorCount;
                WarningCount = result.WarningCount;
                InfoCount = result.InfoCount;
                OtherCount = result.OtherCount;
                ErrorPercent = result.ErrorPercent;
                WarningPercent = result.WarningPercent;
                InfoPercent = result.InfoPercent;
                OtherPercent = result.OtherPercent;

                // Update charts
                LevelsOverTimeSeries = result.LevelsOverTimeSeries;
                TopErrorsSeries = result.TopErrorsSeries;
                LogDistributionSeries = result.LogDistributionSeries;
                TimeHeatmapSeries = result.TimeHeatmapSeries;
                ErrorTrendSeries = result.ErrorTrendSeries;
                SourcesDistributionSeries = result.SourcesDistributionSeries;

                // Update axes
                TimeAxis = result.TimeAxis;
                CountAxis = result.CountAxis;
                DaysAxis = result.DaysAxis;
                HoursAxis = result.HoursAxis;
                SourceAxis = result.SourceAxis;
                ErrorMessageAxis = result.ErrorMessageAxis;

                // Update log statistics
                LogStatistics = result.LogStatistics;

                OnStatisticsUpdated(new StatisticsUpdatedEventArgs(LogStatistics));

                _logger.LogInformation("Statistics updated: {Errors} errors, {Warnings} warnings, {Info} info, {Other} other",
                    ErrorCount, WarningCount, InfoCount, OtherCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating statistics");
                ClearAllCharts();
            }
        }

        /// <summary>
        /// Update IIS-specific statistics
        /// </summary>
        public void UpdateIISStatistics(TabViewModel selectedTab)
        {
            try
            {
                if (selectedTab?.IsThisTabIIS != true) return;

                _logger.LogDebug("Updating IIS statistics for tab: {Title}", selectedTab.Title);

                // Update IIS-specific counts
                ErrorCount = selectedTab.IIS_ErrorCount;
                InfoCount = selectedTab.IIS_InfoCount;
                WarningCount = 0; // IIS logs typically don't have warnings
                OtherCount = selectedTab.IIS_RedirectCount;

                var total = selectedTab.IIS_TotalCount;
                if (total > 0)
                {
                    ErrorPercent = (ErrorCount / (double)total) * 100;
                    InfoPercent = (InfoCount / (double)total) * 100;
                    WarningPercent = 0;
                    OtherPercent = (OtherCount / (double)total) * 100;
                }
                else
                {
                    ErrorPercent = WarningPercent = InfoPercent = OtherPercent = 0;
                }

                // Calculate IIS charts if needed
                CalculateIISCharts(selectedTab);

                _logger.LogInformation("IIS statistics updated: {Errors} errors, {Info} info, {Redirects} redirects",
                    ErrorCount, InfoCount, OtherCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating IIS statistics");
            }
        }

        /// <summary>
        /// Clear all charts and statistics
        /// </summary>
        public void ClearAllCharts()
        {
            try
            {
                _logger.LogDebug("Clearing all charts and statistics");

                LevelsOverTimeSeries = Array.Empty<ISeries>();
                TopErrorsSeries = Array.Empty<ISeries>();
                LogDistributionSeries = Array.Empty<ISeries>();
                TimeHeatmapSeries = Array.Empty<ISeries>();
                ErrorTrendSeries = Array.Empty<ISeries>();
                SourcesDistributionSeries = Array.Empty<ISeries>();

                TimeAxis = Array.Empty<Axis>();
                CountAxis = Array.Empty<Axis>();
                DaysAxis = Array.Empty<Axis>();
                HoursAxis = Array.Empty<Axis>();
                SourceAxis = Array.Empty<Axis>();
                ErrorMessageAxis = Array.Empty<Axis>();

                ErrorCount = WarningCount = InfoCount = OtherCount = 0;
                ErrorPercent = WarningPercent = InfoPercent = OtherPercent = 0;

                LogStatistics = new LogStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing charts");
            }
        }

        #endregion

        #region Private Methods

        private (int ErrorCount, int WarningCount, int InfoCount, int OtherCount, 
                double ErrorPercent, double WarningPercent, double InfoPercent, double OtherPercent, 
                ISeries[] LevelsOverTimeSeries, ISeries[] TopErrorsSeries, ISeries[] LogDistributionSeries, 
                ISeries[] TimeHeatmapSeries, ISeries[] ErrorTrendSeries, ISeries[] SourcesDistributionSeries,
                LogStatistics LogStatistics, Axis[] TimeAxis, Axis[] CountAxis, Axis[] DaysAxis, 
                Axis[] HoursAxis, Axis[] SourceAxis, Axis[] ErrorMessageAxis) 
            CalculateStatisticsAndCharts(List<LogEntry> logEntries)
        {
            try
            {
                if (!logEntries.Any())
                {
                    return (0, 0, 0, 0, 0, 0, 0, 0, 
                           Array.Empty<ISeries>(), Array.Empty<ISeries>(), Array.Empty<ISeries>(),
                           Array.Empty<ISeries>(), Array.Empty<ISeries>(), Array.Empty<ISeries>(),
                           new LogStatistics(), Array.Empty<Axis>(), Array.Empty<Axis>(), 
                           Array.Empty<Axis>(), Array.Empty<Axis>(), Array.Empty<Axis>(), Array.Empty<Axis>());
                }

                // Calculate basic statistics
                var errorCount = logEntries.Count(e => e.Level?.ToLowerInvariant() == "error");
                var warningCount = logEntries.Count(e => e.Level?.ToLowerInvariant() == "warning" || e.Level?.ToLowerInvariant() == "warn");
                var infoCount = logEntries.Count(e => e.Level?.ToLowerInvariant() == "info" || e.Level?.ToLowerInvariant() == "information");
                var otherCount = logEntries.Count - errorCount - warningCount - infoCount;

                var total = logEntries.Count;
                var errorPercent = total > 0 ? (errorCount / (double)total) * 100 : 0;
                var warningPercent = total > 0 ? (warningCount / (double)total) * 100 : 0;
                var infoPercent = total > 0 ? (infoCount / (double)total) * 100 : 0;
                var otherPercent = total > 0 ? (otherCount / (double)total) * 100 : 0;

                // Use chart service to generate charts
                var chartData = _chartService.GenerateCharts(logEntries);

                // Create log statistics
                var logStatistics = new LogStatistics
                {
                    TotalEntries = total,
                    ErrorEntries = errorCount,
                    WarningEntries = warningCount,
                    InfoEntries = infoCount,
                    OtherEntries = otherCount,
                    ErrorPercentage = errorPercent,
                    WarningPercentage = warningPercent,
                    InfoPercentage = infoPercent,
                    OtherPercentage = otherPercent
                };

                return (errorCount, warningCount, infoCount, otherCount,
                       errorPercent, warningPercent, infoPercent, otherPercent,
                       chartData.LevelsOverTimeSeries, chartData.TopErrorsSeries, chartData.LogDistributionSeries,
                       chartData.TimeHeatmapSeries, chartData.ErrorTrendSeries, chartData.SourcesDistributionSeries,
                       logStatistics, chartData.TimeAxis, chartData.CountAxis, chartData.DaysAxis,
                       chartData.HoursAxis, chartData.SourceAxis, chartData.ErrorMessageAxis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating statistics and charts");
                throw;
            }
        }

        private void CalculateIISCharts(TabViewModel selectedTab)
        {
            try
            {
                // Generate IIS-specific charts using chart service
                // This would need to be implemented in the chart service
                _logger.LogDebug("Calculating IIS charts for tab: {Title}", selectedTab.Title);
                
                // For now, clear charts as IIS charts are handled differently
                ClearAllCharts();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating IIS charts");
            }
        }

        private void OnStatisticsUpdated(StatisticsUpdatedEventArgs e)
        {
            StatisticsUpdated?.Invoke(this, e);
        }

        #endregion
    }

    #region Event Args

    public class StatisticsUpdatedEventArgs : EventArgs
    {
        public LogStatistics Statistics { get; }
        public StatisticsUpdatedEventArgs(LogStatistics statistics) => Statistics = statistics;
    }

    #endregion
}