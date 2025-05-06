using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Services;
using Microsoft.Extensions.Logging;

namespace Log_Parser_App.ViewModels
{
    /// <summary>
    ///     Root view‑model that hosts <see cref="MainViewModel"/> and handles UI‑wide concerns
    ///     such as update checks, global filtering UI and dashboard visibility.
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        #region Constants

        private const string FieldTimestamp = "Timestamp";
        private const string FieldLevel     = "Level";
        private const string FieldSource    = "Source";
        private const string FieldMessage   = "Message";

        #endregion

        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly IUpdateService?              _updateService;

        // ─────────────────────────────────── Bindable properties ───────────────────────────────────

        [ObservableProperty] private string          _appVersion          = string.Empty;
        [ObservableProperty] private bool            _isUpdateAvailable;
        [ObservableProperty] private UpdateInfo?     _availableUpdate;
        [ObservableProperty] private bool            _isDashboardVisible;
        [ObservableProperty] private MainViewModel   _mainView = null!;
        [ObservableProperty] private ObservableCollection<FilterCriterion> _filterCriteria = new();

        // ──────────────────────────────── Available filter metadata ───────────────────────────────

        public List<string> AvailableFields { get; } = new() { FieldTimestamp, FieldLevel, FieldSource, FieldMessage };

        public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new()
        {
            { FieldTimestamp, new() { "Equals", "NotEquals", "GreaterThan", "LessThan", "GreaterThanOrEqual", "LessThanOrEqual" } },
            { FieldLevel,     new() { "Equals", "NotEquals", "Contains" } },
            { FieldSource,    new() { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } },
            { FieldMessage,   new() { "Equals", "NotEquals", "Contains", "StartsWith", "EndsWith" } }
        };

        /// <summary>Dynamic value‑lists discovered in the current log set.</summary>
        public Dictionary<string, HashSet<string>> AvailableValuesByField { get; } = new()
        {
            { FieldLevel,  new(StringComparer.OrdinalIgnoreCase) },
            { FieldSource, new(StringComparer.OrdinalIgnoreCase) }
        };

        // ─────────────────────────────────────── Commands ──────────────────────────────────────────

        public IRelayCommand CheckForUpdatesAsyncCommand  { get; private set; } = null!;
        public IRelayCommand AddFilterCriterionCommand    { get; private set; } = null!;
        public IRelayCommand ApplyFiltersCommand          { get; private set; } = null!;
        public IRelayCommand ResetFiltersCommand          { get; private set; } = null!;
        public IRelayCommand RemoveFilterCriterionCommand { get; private set; } = null!;
        public IRelayCommand InstallUpdateAsyncCommand    { get; private set; } = null!;
        public IRelayCommand ToggleDashboardVisibilityCommand { get; private set; } = null!;

        // ───────────────────────────────────── Constructor ─────────────────────────────────────────

        public MainWindowViewModel(
            ILogger<MainWindowViewModel> logger,
            IUpdateService? updateService                       = null,
            ILogParserService? logParserService                 = null,
            IFileService? fileService                           = null,
            IErrorRecommendationService? errorRecommendationService = null,
            MainViewModel? mainView                             = null)
        {
            _logger        = logger ?? throw new ArgumentNullException(nameof(logger));
            _updateService = updateService;

            MainView = mainView ?? CreateMainViewModel(
                logParserService ?? throw new ArgumentNullException(nameof(logParserService)),
                fileService      ?? throw new ArgumentNullException(nameof(fileService)),
                errorRecommendationService ?? throw new ArgumentNullException(nameof(errorRecommendationService)));

            InitializeCommands();
            LoadApplicationVersion();

            if (_updateService != null)
            {
                Dispatcher.UIThread.Post(async () => await ExecuteCheckForUpdatesAsync(), DispatcherPriority.Background);
            }

            _logger.LogInformation("MainWindowViewModel initialised");
        }

        #region Construction helpers

        private MainViewModel CreateMainViewModel(ILogParserService logParserService,
                                                  IFileService fileService,
                                                  IErrorRecommendationService errorRecommendationService)
        {
            var vmLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<MainViewModel>();
            return new MainViewModel(logParserService, vmLogger, fileService, errorRecommendationService);
        }

        private UpdateViewModel CreateUpdateViewModel()
        {
            var updateVmLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<UpdateViewModel>();
            return _updateService == null ? new UpdateViewModel(null!, updateVmLogger) : new UpdateViewModel(_updateService, updateVmLogger);
        }

        #endregion

        #region Command initialisation

        private void InitializeCommands()
        {
            CheckForUpdatesAsyncCommand      = new AsyncRelayCommand(ExecuteCheckForUpdatesAsync);
            AddFilterCriterionCommand        = new RelayCommand(ExecuteAddFilterCriterion);
            ApplyFiltersCommand              = new AsyncRelayCommand(ExecuteApplyFiltersAsync);
            ResetFiltersCommand              = new AsyncRelayCommand(ExecuteResetFiltersAsync);
            RemoveFilterCriterionCommand     = new RelayCommand<FilterCriterion>(ExecuteRemoveFilterCriterion);
            InstallUpdateAsyncCommand        = new AsyncRelayCommand(ExecuteInstallUpdateAsync);
            ToggleDashboardVisibilityCommand = new RelayCommand(() => IsDashboardVisible = !IsDashboardVisible);
        }

        #endregion

        #region Update logic

        private void LoadApplicationVersion()
        {
            var name = Assembly.GetExecutingAssembly().GetName();
            AppVersion = name.Version?.ToString() ?? "Unknown";
        }

        private async Task ExecuteCheckForUpdatesAsync()
        {
            if (_updateService == null) return;

            try
            {
                var info = await _updateService.CheckForUpdatesAsync();
                if (info != null)
                {
                    AvailableUpdate    = info;
                    IsUpdateAvailable = true;
                    _logger.LogInformation("Update available: {Version}", info.Version);
                    CreateUpdateViewModel().ShowUpdateNotification(info);
                }
                else
                {
                    IsUpdateAvailable = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check for updates");
                IsUpdateAvailable = false;
            }
        }

        private async Task ExecuteInstallUpdateAsync()
        {
            if (AvailableUpdate == null || _updateService == null) return;
            try
            {
                _logger.LogInformation("Installing update {Version}", AvailableUpdate.Version);
                // await _updateService.DownloadAndInstallUpdateAsync(AvailableUpdate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to install update");
            }
        }
        #endregion

        #region Filtering UI helpers

        private void ExecuteAddFilterCriterion()
        {
            var field = AvailableFields.First();
            var op    = OperatorsByFieldType[field].First();
            FilterCriteria.Add(new FilterCriterion { SelectedField = field, SelectedOperator = op });
        }

        private void ExecuteRemoveFilterCriterion(FilterCriterion? criterion)
        {
            if (criterion != null) FilterCriteria.Remove(criterion);
        }

        /// <summary>
        ///     Applies every user‑defined <see cref="FilterCriterion"/> to the currently selected tab's entries.
        /// </summary>
        private async Task ExecuteApplyFiltersAsync()
        {
            var currentTab = MainView.SelectedTab;
            if (currentTab == null || currentTab.LogEntries.Count == 0)
            {
                MainView.StatusMessage = "No log entries to filter";
                return;
            }

            if (FilterCriteria.Count == 0 || FilterCriteria.Any(c => string.IsNullOrWhiteSpace(c.SelectedField) || string.IsNullOrWhiteSpace(c.SelectedOperator)))
            {
                MainView.StatusMessage = "Please configure all filter criteria";
                return;
            }

            MainView.StatusMessage = "Applying filters…";
            MainView.IsLoading     = true;

            try
            {
                var toFilter = currentTab.LogEntries.ToList();
                var filtered = await Task.Run(() =>
                {
                    IEnumerable<LogEntry> working = toFilter;
                    foreach (var c in FilterCriteria)
                    {
                        if (string.IsNullOrWhiteSpace(c.SelectedField) || string.IsNullOrWhiteSpace(c.SelectedOperator) || c.Value is null)
                        {
                            _logger.LogWarning("Skipping incomplete filter criterion: {Field} {Operator} {Value}", c.SelectedField, c.SelectedOperator, c.Value);
                            continue;
                        }
                        working = ApplySingleFilter(working, c);
                    }
                    return working.ToList();
                });

                currentTab.FilteredLogEntries.Clear();
                foreach (var e in filtered) currentTab.FilteredLogEntries.Add(e);
                MainView.StatusMessage = $"Filters applied. Found {currentTab.FilteredLogEntries.Count} entries";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying filters");
                MainView.StatusMessage = $"Error applying filters: {ex.Message}";
            }
            finally
            {
                MainView.IsLoading = false;
            }
        }

        private static IEnumerable<LogEntry> ApplySingleFilter(IEnumerable<LogEntry> entries, FilterCriterion c)
        {
            return c.SelectedField switch
            {
                FieldLevel => c.SelectedOperator switch
                {
                    "Equals"   => entries.Where(e => e.Level.Equals(c.Value, StringComparison.OrdinalIgnoreCase)),
                    "NotEquals"=> entries.Where(e => !e.Level.Equals(c.Value, StringComparison.OrdinalIgnoreCase)),
                    "Contains" => entries.Where(e => e.Level.Contains(c.Value!, StringComparison.OrdinalIgnoreCase)),
                    _           => entries
                },
                FieldSource => c.SelectedOperator switch
                {
                    "Equals"     => entries.Where(e => e.Source?.Equals(c.Value, StringComparison.OrdinalIgnoreCase) ?? false),
                    "NotEquals"  => entries.Where(e => !(e.Source?.Equals(c.Value, StringComparison.OrdinalIgnoreCase) ?? false)),
                    "Contains"   => entries.Where(e => e.Source?.Contains(c.Value!, StringComparison.OrdinalIgnoreCase) ?? false),
                    "StartsWith" => entries.Where(e => e.Source?.StartsWith(c.Value!, StringComparison.OrdinalIgnoreCase) ?? false),
                    "EndsWith"   => entries.Where(e => e.Source?.EndsWith(c.Value!, StringComparison.OrdinalIgnoreCase) ?? false),
                    _            => entries
                },
                FieldMessage => c.SelectedOperator switch
                {
                    "Equals"     => entries.Where(e => e.Message.Equals(c.Value, StringComparison.OrdinalIgnoreCase)),
                    "NotEquals"  => entries.Where(e => !e.Message.Equals(c.Value, StringComparison.OrdinalIgnoreCase)),
                    "Contains"   => entries.Where(e => e.Message.Contains(c.Value!, StringComparison.OrdinalIgnoreCase)),
                    "StartsWith" => entries.Where(e => e.Message.StartsWith(c.Value!, StringComparison.OrdinalIgnoreCase)),
                    "EndsWith"   => entries.Where(e => e.Message.EndsWith(c.Value!, StringComparison.OrdinalIgnoreCase)),
                    _            => entries
                },
                FieldTimestamp when DateTimeOffset.TryParse(c.Value, out var dateVal) => c.SelectedOperator switch
                {
                    "Equals"              => entries.Where(e => e.Timestamp.Date == dateVal.Date),
                    "NotEquals"           => entries.Where(e => e.Timestamp.Date != dateVal.Date),
                    "GreaterThan"         => entries.Where(e => e.Timestamp >  dateVal),
                    "LessThan"            => entries.Where(e => e.Timestamp <  dateVal),
                    "GreaterThanOrEqual"  => entries.Where(e => e.Timestamp >= dateVal),
                    "LessThanOrEqual"     => entries.Where(e => e.Timestamp <= dateVal),
                    _                      => entries
                },
                _ => entries
            };
        }

        private async Task ExecuteResetFiltersAsync()
        {
            var currentTab = MainView.SelectedTab;
            if (currentTab == null || currentTab.LogEntries.Count == 0)
            {
                MainView.StatusMessage = "No log entries to reset";
                return;
            }

            MainView.StatusMessage = "Resetting filters…";
            MainView.IsLoading     = true;

            try
            {
                var all = currentTab.LogEntries.ToList();
                await Task.Run(() => { }); // keep async signature in case of future CPU work
                currentTab.FilteredLogEntries.Clear();
                foreach (var e in all) currentTab.FilteredLogEntries.Add(e);
                MainView.StatusMessage = $"Filters reset. Showing all {currentTab.FilteredLogEntries.Count} entries";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting filters");
                MainView.StatusMessage = $"Error resetting filters: {ex.Message}";
            }
            finally
            {
                MainView.IsLoading = false;
            }
        }

        #endregion

        #region Dynamic value cache helpers

        public void UpdateAvailableFilterValues(IEnumerable<LogEntry> entries)
        {
            if (entries == null) return;
            var levels  = new HashSet<string>(entries.Select(e => e.Level).Where(l => !string.IsNullOrWhiteSpace(l)), StringComparer.OrdinalIgnoreCase);
            var sources = new HashSet<string>(entries.Select(e => e.Source).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);

            bool changed = UpdateHashSet(AvailableValuesByField[FieldLevel], levels) |
                           UpdateHashSet(AvailableValuesByField[FieldSource], sources);

            if (changed)
            {
                _logger.LogInformation("Updated filter value caches");
            }
        }

        private static bool UpdateHashSet(HashSet<string> existing, HashSet<string> newer)
        {
            if (existing.SetEquals(newer)) return false;
            existing.Clear();
            foreach (var v in newer) existing.Add(v);
            return true;
        }

        #endregion
    }
}
