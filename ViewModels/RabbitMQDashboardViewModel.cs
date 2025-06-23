namespace Log_Parser_App.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Avalonia.Threading;
    using Log_Parser_App.Interfaces;
    using Log_Parser_App.Models;
    using Log_Parser_App.Models.Analytics;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// ViewModel for RabbitMQ dashboard analytics widgets
    /// </summary>
    public class RabbitMQDashboardViewModel : ViewModelBase
    {
        private readonly IRabbitMQAnalyticsService _analyticsService;
        private readonly ILogger<RabbitMQDashboardViewModel> _logger;
        private CancellationTokenSource? _cancellationTokenSource;

        // Loading states
        private bool _isLoading = false;
        private bool _isLoadingActiveConsumers = false;
        private bool _isLoadingCriticalErrors = false;
        private bool _isLoadingAccountActivity = false;
        private bool _isLoadingAnomalies = false;
        private AnomalyInsightInfo? _anomaliesInsight;

        /// <summary>
        /// Column settings for adjustable column widths
        /// </summary>
        public ColumnSettings ColumnSettings { get; }

        /// <summary>
        /// Initializes a new instance of the RabbitMQDashboardViewModel
        /// </summary>
        /// <param name="analyticsService">Analytics service for calculations</param>
        /// <param name="logger">Logger instance</param>
        public RabbitMQDashboardViewModel(
            IRabbitMQAnalyticsService analyticsService, 
            ILogger<RabbitMQDashboardViewModel> logger)
        {
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Initialize collections
            ActiveConsumers = new ObservableCollection<ConsumerStatusInfo>();
            RecentCriticalErrors = new ObservableCollection<CriticalErrorInfo>();
            AccountActivity = new ObservableCollection<AccountActivityInfo>();
            WarningsTimeline = new ObservableCollection<WarningTimelineInfo>();

            // Initialize column settings
            ColumnSettings = new ColumnSettings();
        }

        #region Properties

        /// <summary>
        /// Collection of active consumers with their status information
        /// </summary>
        public ObservableCollection<ConsumerStatusInfo> ActiveConsumers { get; }

        /// <summary>
        /// Collection of recent critical errors
        /// </summary>
        public ObservableCollection<CriticalErrorInfo> RecentCriticalErrors { get; }

        /// <summary>
        /// Collection of account activity information
        /// </summary>
        public ObservableCollection<AccountActivityInfo> AccountActivity { get; }

        /// <summary>
        /// Collection of warnings timeline data
        /// </summary>
        public ObservableCollection<WarningTimelineInfo> WarningsTimeline { get; }

        /// <summary>
        /// Anomaly insights information
        /// </summary>
        public AnomalyInsightInfo? AnomaliesInsight
        {
            get => _anomaliesInsight;
            set => SetProperty(ref _anomaliesInsight, value);
        }

        /// <summary>
        /// Overall loading state for all analytics
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Summary statistics for active consumers
        /// </summary>
        public int ActiveConsumerCount => ActiveConsumers.Count(c => c.Status == ConsumerStatus.Active);
        public int ErrorConsumerCount => ActiveConsumers.Count(c => c.Status == ConsumerStatus.Error);
        public int InactiveConsumerCount => ActiveConsumers.Count(c => c.Status == ConsumerStatus.Inactive);

        /// <summary>
        /// Indicates if anomalies widget should be visible
        /// </summary>
        public bool ShowAnomaliesWidget => AnomaliesInsight?.HasAnomalies == true;

        // Loading states for individual widgets
        public bool IsLoadingActiveConsumers
        {
            get => _isLoadingActiveConsumers;
            set => SetProperty(ref _isLoadingActiveConsumers, value);
        }

        public bool IsLoadingCriticalErrors
        {
            get => _isLoadingCriticalErrors;
            set => SetProperty(ref _isLoadingCriticalErrors, value);
        }

        public bool IsLoadingAccountActivity
        {
            get => _isLoadingAccountActivity;
            set => SetProperty(ref _isLoadingAccountActivity, value);
        }

        public bool IsLoadingAnomalies
        {
            get => _isLoadingAnomalies;
            set => SetProperty(ref _isLoadingAnomalies, value);
        }

        // Count properties for XAML binding
        public int ActiveConsumersCount => ActiveConsumers.Count;
        public int CriticalErrorsCount => RecentCriticalErrors.Count;
        public int HighRiskAccountsCount => AccountActivity.Count(a => a.RiskLevel == RiskLevel.High);
        public int AnomaliesCount => AnomaliesInsight?.AnomalyCount ?? 0;

        // Collection aliases for XAML binding
        public ObservableCollection<AccountActivityInfo> AccountActivities => AccountActivity;
        public ObservableCollection<AnomalyInsightInfo> AnomalyInsights => 
            AnomaliesInsight != null 
                ? new ObservableCollection<AnomalyInsightInfo> { AnomaliesInsight } 
                : new ObservableCollection<AnomalyInsightInfo>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes all analytics data with the provided log entries
        /// </summary>
        /// <param name="entries">RabbitMQ log entries to analyze</param>
        /// <returns>Task representing the refresh operation</returns>
        public async Task RefreshAnalyticsAsync(IEnumerable<RabbitMqLogEntry> entries)
        {
            if (entries == null)
            {
                _logger.LogWarning("Null entries provided to RefreshAnalyticsAsync");
                return;
            }

            var entriesArray = entries.ToArray();
            if (!entriesArray.Any())
            {
                _logger.LogDebug("No entries provided for analytics refresh");
                await ClearAllDataAsync();
                return;
            }

            _logger.LogDebug("Starting analytics refresh with {EntryCount} entries", entriesArray.Length);

            // Cancel any previous operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            try
            {
                IsLoading = true;

                // Execute all analytics in parallel for better performance
                var refreshTasks = new[]
                {
                    RefreshActiveConsumersAsync(entriesArray, cancellationToken),
                    RefreshCriticalErrorsAsync(entriesArray, cancellationToken),
                    RefreshAccountActivityAsync(entriesArray, cancellationToken),
                    RefreshWarningsTimelineAsync(entriesArray, cancellationToken),
                    RefreshAnomaliesInsightAsync(entriesArray, cancellationToken)
                };

                await Task.WhenAll(refreshTasks);

                // Update summary properties after all data is loaded
                OnPropertyChanged(nameof(ActiveConsumerCount));
                OnPropertyChanged(nameof(ErrorConsumerCount));
                OnPropertyChanged(nameof(InactiveConsumerCount));
                OnPropertyChanged(nameof(ShowAnomaliesWidget));
                OnPropertyChanged(nameof(ActiveConsumersCount));
                OnPropertyChanged(nameof(CriticalErrorsCount));
                OnPropertyChanged(nameof(HighRiskAccountsCount));
                OnPropertyChanged(nameof(AnomaliesCount));

                _logger.LogDebug("Analytics refresh completed successfully");
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Analytics refresh was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during analytics refresh");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Clears all analytics data
        /// </summary>
        /// <returns>Task representing the clear operation</returns>
        public async Task ClearAllDataAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ActiveConsumers.Clear();
                RecentCriticalErrors.Clear();
                AccountActivity.Clear();
                WarningsTimeline.Clear();
                AnomaliesInsight = null;

                // Update summary properties
                OnPropertyChanged(nameof(ActiveConsumerCount));
                OnPropertyChanged(nameof(ErrorConsumerCount));
                OnPropertyChanged(nameof(InactiveConsumerCount));
                OnPropertyChanged(nameof(ShowAnomaliesWidget));
                OnPropertyChanged(nameof(ActiveConsumersCount));
                OnPropertyChanged(nameof(CriticalErrorsCount));
                OnPropertyChanged(nameof(HighRiskAccountsCount));
                OnPropertyChanged(nameof(AnomaliesCount));
                OnPropertyChanged(nameof(AccountActivities));
                OnPropertyChanged(nameof(AnomalyInsights));
            });
        }

        #endregion

        #region Private Methods

        private async Task RefreshActiveConsumersAsync(RabbitMqLogEntry[] entries, CancellationToken cancellationToken)
        {
            try
            {
                IsLoadingActiveConsumers = true;
                var consumers = await _analyticsService.GetActiveConsumersAsync(entries, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ActiveConsumers.Clear();
                    foreach (var consumer in consumers)
                    {
                        ActiveConsumers.Add(consumer);
                    }
                    OnPropertyChanged(nameof(ActiveConsumersCount));
                });

                _logger.LogDebug("Refreshed active consumers: {Count} items", consumers.Length);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing active consumers");
            }
            finally
            {
                IsLoadingActiveConsumers = false;
            }
        }

        private async Task RefreshCriticalErrorsAsync(RabbitMqLogEntry[] entries, CancellationToken cancellationToken)
        {
            try
            {
                IsLoadingCriticalErrors = true;
                var errors = await _analyticsService.GetRecentCriticalErrorsAsync(entries, 5, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    RecentCriticalErrors.Clear();
                    foreach (var error in errors)
                    {
                        RecentCriticalErrors.Add(error);
                    }
                    OnPropertyChanged(nameof(CriticalErrorsCount));
                });

                _logger.LogDebug("Refreshed critical errors: {Count} items", errors.Length);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing critical errors");
            }
            finally
            {
                IsLoadingCriticalErrors = false;
            }
        }

        private async Task RefreshAccountActivityAsync(RabbitMqLogEntry[] entries, CancellationToken cancellationToken)
        {
            try
            {
                IsLoadingAccountActivity = true;
                var activity = await _analyticsService.GetAccountActivityAnalysisAsync(entries, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AccountActivity.Clear();
                    foreach (var account in activity)
                    {
                        AccountActivity.Add(account);
                    }
                    OnPropertyChanged(nameof(HighRiskAccountsCount));
                    OnPropertyChanged(nameof(AccountActivities));
                });

                _logger.LogDebug("Refreshed account activity: {Count} items", activity.Length);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing account activity");
            }
            finally
            {
                IsLoadingAccountActivity = false;
            }
        }

        private async Task RefreshWarningsTimelineAsync(RabbitMqLogEntry[] entries, CancellationToken cancellationToken)
        {
            try
            {
                var timeline = await _analyticsService.GetSystemWarningsTimelineAsync(entries, cancellationToken);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WarningsTimeline.Clear();
                    foreach (var item in timeline)
                    {
                        WarningsTimeline.Add(item);
                    }
                });

                _logger.LogDebug("Refreshed warnings timeline: {Count} items", timeline.Length);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing warnings timeline");
            }
        }

        private async Task RefreshAnomaliesInsightAsync(RabbitMqLogEntry[] entries, CancellationToken cancellationToken)
        {
            try
            {
                IsLoadingAnomalies = true;
                var insight = await _analyticsService.GetAnomaliesInsightAsync(entries, cancellationToken);
                
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AnomaliesInsight = insight;
                    OnPropertyChanged(nameof(AnomaliesCount));
                    OnPropertyChanged(nameof(AnomalyInsights));
                });

                _logger.LogDebug("Refreshed anomalies insight: {AnomalyCount} anomalies", insight.AnomalyCount);
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing anomalies insight");
            }
            finally
            {
                IsLoadingAnomalies = false;
            }
        }

        #endregion
    }
} 