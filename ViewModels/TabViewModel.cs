using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Log_Parser_App.Models;
using Log_Parser_App.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Log_Parser_App.ViewModels
{
	#region Class: TabViewModel

	public partial class TabViewModel : ObservableObject
	{

		#region Fields: Private

		private string _filePath;
		private string _title;
		private List<LogEntry> _logEntries;
		private List<IisLogEntry> _iisLogEntries;
		// RabbitMQ entries collection
		private List<RabbitMqLogEntry> _rabbitMqLogEntries;
		private bool _isSelected;
		private bool _isErrorsOnly;

	#endregion

		#region Properties: Public

		public string FilePath {
			get => _filePath;
			set => SetProperty(ref _filePath, value);
		}

		public string Title {
			get => _title;
			set => SetProperty(ref _title, value);
		}

		public List<LogEntry> LogEntries {
			get => _logEntries;
			set => SetProperty(ref _logEntries, value);
		}

		public List<IisLogEntry> IISLogEntries {
			get => _iisLogEntries;
			set {
				_iisLogEntries = value;
				OnPropertyChanged();
			}
		}

		public List<RabbitMqLogEntry> RabbitMQLogEntries {
			get => _rabbitMqLogEntries;
			set {
				_rabbitMqLogEntries = value;
				OnPropertyChanged();
			}
		}

			public LogFormatType LogType { get; }

	// New direct properties for tab type checking

		public bool IsThisTabIIS => LogType == LogFormatType.IIS;

		public bool IsThisTabStandard => LogType == LogFormatType.Standard;

			public bool IsThisTabRabbitMQ => LogType == LogFormatType.RabbitMQ;

	// Combined property for UI binding - both Standard and RabbitMQ use the same LogEntry structure

	public bool IsThisTabStandardOrRabbitMQ => LogType == LogFormatType.Standard || LogType == LogFormatType.RabbitMQ;

		public bool IsSelected {
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}

		public bool IsErrorsOnly {
			get => _isErrorsOnly;
			set {
				if (SetProperty(ref _isErrorsOnly, value)) {
					// Trigger error filtering change event
					ErrorsOnlyFilterChanged?.Invoke(this, new ErrorsOnlyFilterChangedEventArgs(value, LogType));
				}
			}
		}

		// Event for notifying when errors-only filter changes
		public event EventHandler<ErrorsOnlyFilterChangedEventArgs>? ErrorsOnlyFilterChanged;

		private bool _isIISErrorsOnly;
		public bool IsIISErrorsOnly
		{
			get => _isIISErrorsOnly;
			set
			{
				if (SetProperty(ref _isIISErrorsOnly, value))
				{
					ExecuteApplyIISFilters();
				}
			}
		}

		// --- IIS Filtering Properties and Commands ---

		public ObservableCollection<IISFilterCriterion> IISFilterCriteria { get; }

		public ObservableCollection<IisLogEntry> FilteredIISLogEntries { get; }

		// RabbitMQ Filtering

		public ObservableCollection<RabbitMqLogEntry> FilteredRabbitMQLogEntries { get; }

		// Statistics for IIS

		public int IIS_TotalCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count : 0;

		public int IIS_ErrorCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => (e.HttpStatus ?? 0) >= 400) : 0;

		public int IIS_InfoCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => (e.HttpStatus ?? 0) >= 200 && (e.HttpStatus ?? 0) < 300) : 0;

		public int IIS_RedirectCount => LogType == LogFormatType.IIS ? FilteredIISLogEntries.Count(e => (e.HttpStatus ?? 0) >= 300 && (e.HttpStatus ?? 0) < 400) : 0;

		// Statistics for RabbitMQ

		public int RabbitMQ_TotalCount => LogType == LogFormatType.RabbitMQ ? FilteredRabbitMQLogEntries.Count : 0;

		public int RabbitMQ_ErrorCount => LogType == LogFormatType.RabbitMQ
			? FilteredRabbitMQLogEntries.Count(e => e.EffectiveLevel != null && (e.EffectiveLevel.ToLower().Contains("error") || e.EffectiveLevel.ToLower().Contains("fatal")))
			: 0;

		public int RabbitMQ_WarningCount =>
			LogType == LogFormatType.RabbitMQ ? FilteredRabbitMQLogEntries.Count(e => e.EffectiveLevel != null && e.EffectiveLevel.ToLower().Contains("warn")) : 0;

		public int RabbitMQ_InfoCount =>
			LogType == LogFormatType.RabbitMQ ? FilteredRabbitMQLogEntries.Count(e => e.EffectiveLevel != null && e.EffectiveLevel.ToLower().Contains("info")) : 0;

		public IRelayCommand AddIISFilterCriterionCommand { get; }

		public IRelayCommand<IISFilterCriterion> RemoveIISFilterCriterionCommand { get; }

		public IRelayCommand ApplyIISFiltersCommand { get; }

		public IRelayCommand ResetIISFiltersCommand { get; }

		public IRelayCommand ExportIisLogsToCsvCommand { get; }

		// --- IIS Sorting ---
		
		public IRelayCommand<string> SortIISCommand { get; }
		
		private string? _currentIISSortField;
		private bool _isIISAscending = true;
		
		public string? CurrentIISSortField {
			get => _currentIISSortField;
			set => SetProperty(ref _currentIISSortField, value);
		}
		
		public bool IsIISAscending {
			get => _isIISAscending;
			set => SetProperty(ref _isIISAscending, value);
		}

		// --- End IIS Filtering ---
		// --- Standard Filtering Properties and Commands ---

		public ObservableCollection<FilterCriterion> FilterCriteria { get; }

		public ObservableCollection<LogEntry> FilteredLogEntries { get; }

		public IRelayCommand AddFilterCriteriaCommand { get; }

		public IRelayCommand<FilterCriterion> RemoveFilterCriterionCommand { get; }

		public IRelayCommand ApplyFiltersCommand { get; }

		public IRelayCommand ResetFiltersCommand { get; }

		// --- End Standard Filtering ---
		// Свойства для фильтрации

		public Dictionary<string, List<string>> AvailableValuesByField { get; } = new();

		public Dictionary<string, List<string>> OperatorsByFieldType { get; } = new();

		public List<string> MasterAvailableFields { get; } = new();

		// RabbitMQ Filter Commands
		public ObservableCollection<FilterCriterion> RabbitMQFilterCriteria { get; }
		public IRelayCommand AddRabbitMQFilterCriterionCommand { get; }
		public IRelayCommand<FilterCriterion> RemoveRabbitMQFilterCriterionCommand { get; }
		public IRelayCommand ApplyRabbitMQFiltersCommand { get; }
		public IRelayCommand ResetRabbitMQFiltersCommand { get; }

		#endregion

		#region Constructors: Public

		public TabViewModel(string filePath, string title, List<LogEntry> logEntries) {
			_filePath = filePath;
			_title = title;
			_logEntries = logEntries;
			_iisLogEntries = new List<IisLogEntry>();
			_rabbitMqLogEntries = new List<RabbitMqLogEntry>();
			LogType = LogFormatType.Standard;
			_isSelected = false;
			// Initialize all collections - even if not used by this log type
			IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
			FilteredIISLogEntries = new ObservableCollection<IisLogEntry>();
			FilteredRabbitMQLogEntries = new ObservableCollection<RabbitMqLogEntry>();
			AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
			RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
			ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
			ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
			ExportIisLogsToCsvCommand = new RelayCommand(ExecuteExportIisLogsToCsv);
			SortIISCommand = new RelayCommand<string>(ExecuteSortIIS);
			// Initialize Standard Filtering related collections and commands
			FilterCriteria = new ObservableCollection<FilterCriterion>();
			FilteredLogEntries = new ObservableCollection<LogEntry>(logEntries); // Initially populate with all log entries
			AddFilterCriteriaCommand = new RelayCommand(ExecuteAddFilterCriterion);
			RemoveFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveFilterCriterion);
			ApplyFiltersCommand = new RelayCommand(ExecuteApplyFilters);
			ResetFiltersCommand = new RelayCommand(ExecuteResetFilters);
			// Initialize RabbitMQ Filtering related collections and commands
			RabbitMQFilterCriteria = new ObservableCollection<FilterCriterion>();
			AddRabbitMQFilterCriterionCommand = new RelayCommand(ExecuteAddRabbitMQFilterCriterion);
			RemoveRabbitMQFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveRabbitMQFilterCriterion);
			ApplyRabbitMQFiltersCommand = new RelayCommand(ExecuteApplyRabbitMQFilters);
			ResetRabbitMQFiltersCommand = new RelayCommand(ExecuteResetRabbitMQFilters);
			InitializeFilterFields();
		}

		public TabViewModel(string filePath, string title, List<IisLogEntry> iisLogEntries) {
			_filePath = filePath;
			_title = title;
			_logEntries = new List<LogEntry>();
			_iisLogEntries = iisLogEntries;
			_rabbitMqLogEntries = new List<RabbitMqLogEntry>();
			LogType = LogFormatType.IIS;
			_isSelected = false;
			// Initialize all collections
			IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
			FilteredIISLogEntries = new ObservableCollection<IisLogEntry>(iisLogEntries); // Initially populate with all IIS entries
			FilteredRabbitMQLogEntries = new ObservableCollection<RabbitMqLogEntry>();
			AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
			RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
			ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
			ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
			ExportIisLogsToCsvCommand = new RelayCommand(ExecuteExportIisLogsToCsv);
			SortIISCommand = new RelayCommand<string>(ExecuteSortIIS);
			// Initialize Standard Filtering related collections and commands
			FilterCriteria = new ObservableCollection<FilterCriterion>();
			FilteredLogEntries = new ObservableCollection<LogEntry>(); // Empty for IIS-specific tabs
			AddFilterCriteriaCommand = new RelayCommand(ExecuteAddFilterCriterion);
			RemoveFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveFilterCriterion);
			ApplyFiltersCommand = new RelayCommand(ExecuteApplyFilters);
			ResetFiltersCommand = new RelayCommand(ExecuteResetFilters);
			// Initialize RabbitMQ Filtering related collections and commands
			RabbitMQFilterCriteria = new ObservableCollection<FilterCriterion>();
			AddRabbitMQFilterCriterionCommand = new RelayCommand(ExecuteAddRabbitMQFilterCriterion);
			RemoveRabbitMQFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveRabbitMQFilterCriterion);
			ApplyRabbitMQFiltersCommand = new RelayCommand(ExecuteApplyRabbitMQFilters);
			ResetRabbitMQFiltersCommand = new RelayCommand(ExecuteResetRabbitMQFilters);
			InitializeFilterFields();
		}

		// Constructor for RabbitMQ and other log types with explicit LogFormatType

		public TabViewModel(string filePath, string title, List<LogEntry> logEntries, LogFormatType logType) {
			_filePath = filePath;
			_title = title;
			_logEntries = logEntries;
			_iisLogEntries = new List<IisLogEntry>();
			_rabbitMqLogEntries = new List<RabbitMqLogEntry>();
			LogType = logType;
			_isSelected = false;
			// Initialize all collections
			IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
			FilteredIISLogEntries = new ObservableCollection<IisLogEntry>();
			FilteredRabbitMQLogEntries = new ObservableCollection<RabbitMqLogEntry>();
			AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
			RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
			ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
			ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
			ExportIisLogsToCsvCommand = new RelayCommand(ExecuteExportIisLogsToCsv);
			SortIISCommand = new RelayCommand<string>(ExecuteSortIIS);
			// Initialize Standard Filtering related collections and commands
			FilterCriteria = new ObservableCollection<FilterCriterion>();
			FilteredLogEntries = new ObservableCollection<LogEntry>(logEntries); // Initially populate with all entries
			AddFilterCriteriaCommand = new RelayCommand(ExecuteAddFilterCriterion);
			RemoveFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveFilterCriterion);
			ApplyFiltersCommand = new RelayCommand(ExecuteApplyFilters);
			ResetFiltersCommand = new RelayCommand(ExecuteResetFilters);
			// Initialize RabbitMQ Filtering related collections and commands
			RabbitMQFilterCriteria = new ObservableCollection<FilterCriterion>();
			AddRabbitMQFilterCriterionCommand = new RelayCommand(ExecuteAddRabbitMQFilterCriterion);
			RemoveRabbitMQFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveRabbitMQFilterCriterion);
			ApplyRabbitMQFiltersCommand = new RelayCommand(ExecuteApplyRabbitMQFilters);
			ResetRabbitMQFiltersCommand = new RelayCommand(ExecuteResetRabbitMQFilters);
			InitializeFilterFields();
		}

		// Constructor specifically for RabbitMQ logs 

		public TabViewModel(string filePath, string title, List<RabbitMqLogEntry> rabbitMqLogEntries) {
			_filePath = filePath;
			_title = title;
			_logEntries = new List<LogEntry>();
			_iisLogEntries = new List<IisLogEntry>();
			_rabbitMqLogEntries = rabbitMqLogEntries;
			LogType = LogFormatType.RabbitMQ;
			_isSelected = false;
			// Initialize all collections
			IISFilterCriteria = new ObservableCollection<IISFilterCriterion>();
			FilteredIISLogEntries = new ObservableCollection<IisLogEntry>();
			FilteredRabbitMQLogEntries = new ObservableCollection<RabbitMqLogEntry>(rabbitMqLogEntries); // Initially populate with all RabbitMQ entries
			AddIISFilterCriterionCommand = new RelayCommand(ExecuteAddIISFilterCriterion);
			RemoveIISFilterCriterionCommand = new RelayCommand<IISFilterCriterion>(ExecuteRemoveIISFilterCriterion);
			ApplyIISFiltersCommand = new RelayCommand(ExecuteApplyIISFilters);
			ResetIISFiltersCommand = new RelayCommand(ExecuteResetIISFilters);
			ExportIisLogsToCsvCommand = new RelayCommand(ExecuteExportIisLogsToCsv);
			SortIISCommand = new RelayCommand<string>(ExecuteSortIIS);
			// Initialize Standard Filtering related collections and commands
			FilterCriteria = new ObservableCollection<FilterCriterion>();
			FilteredLogEntries = new ObservableCollection<LogEntry>(); // Empty for RabbitMQ-specific tabs
			AddFilterCriteriaCommand = new RelayCommand(ExecuteAddFilterCriterion);
			RemoveFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveFilterCriterion);
			ApplyFiltersCommand = new RelayCommand(ExecuteApplyFilters);
			ResetFiltersCommand = new RelayCommand(ExecuteResetFilters);
			// Initialize RabbitMQ Filtering related collections and commands
			RabbitMQFilterCriteria = new ObservableCollection<FilterCriterion>();
			AddRabbitMQFilterCriterionCommand = new RelayCommand(ExecuteAddRabbitMQFilterCriterion);
			RemoveRabbitMQFilterCriterionCommand = new RelayCommand<FilterCriterion>(ExecuteRemoveRabbitMQFilterCriterion);
			ApplyRabbitMQFiltersCommand = new RelayCommand(ExecuteApplyRabbitMQFilters);
			ResetRabbitMQFiltersCommand = new RelayCommand(ExecuteResetRabbitMQFilters);
			InitializeFilterFields();
		}



		#endregion

		#region Methods: Private

		private void ExecuteAddIISFilterCriterion() {
			if (LogType != LogFormatType.IIS)
				return;
			var newCriterion = new IISFilterCriterion { ParentViewModel = this };
			IISFilterCriteria.Add(newCriterion);
		}

		private void ExecuteRemoveIISFilterCriterion(IISFilterCriterion? criterion) {
			if (LogType != LogFormatType.IIS)
				return;
			if (criterion != null) {
				IISFilterCriteria.Remove(criterion);
			}
		}

		private void ExecuteApplyIISFilters() {
			IEnumerable<IisLogEntry> filtered = IISLogEntries;

			if (IsIISErrorsOnly)
			{
				filtered = filtered.Where(e => e.HttpStatus.HasValue && e.HttpStatus.Value >= 400);
			}

			if (IISFilterCriteria.Any())
			{
				filtered = filtered.Where(entry => IISFilterCriteria.All(criterion =>
				{
					if (string.IsNullOrWhiteSpace(criterion.SelectedOperator))
					{
						return true;
					}

					var valueToCompare = criterion.UseManualInput ? criterion.ManualValue : criterion.Value;

					if (string.IsNullOrWhiteSpace(valueToCompare))
					{
						return true;
					}

					return MatchCriterion(entry, criterion, valueToCompare.ToLower(),
						double.TryParse(valueToCompare, out var num) ? num : (double?)null);
				}));
			}

			FilteredIISLogEntries.Clear();
			foreach (var entry in filtered)
			{
				FilteredIISLogEntries.Add(entry);
			}
			ApplySortToIISEntries();
		}

		private bool IsNumericField(IISLogField field) {
			return field == IISLogField.TimeTaken || field == IISLogField.Port || field == IISLogField.HttpStatus || field == IISLogField.Win32Status;
		}

		private bool MatchCriterion(IisLogEntry entry, IISFilterCriterion criterion, string lowerFilterValue, double? numericFilterValue) {
			string? valueToCompareLower = GetPropertyValue(entry, criterion.SelectedField)?.ToString()?.ToLowerInvariant();
			if (valueToCompareLower == null && !string.IsNullOrEmpty(lowerFilterValue))
				return false;
			if (valueToCompareLower == null && string.IsNullOrEmpty(lowerFilterValue))
				return true;
			if (valueToCompareLower == null)
				return false;
			switch (criterion.SelectedOperator) {
				case "Equals":
					return valueToCompareLower.Equals(lowerFilterValue);
				case "NotEquals":
					return !valueToCompareLower.Equals(lowerFilterValue);
				case "Contains":
					return valueToCompareLower.Contains(lowerFilterValue);
				case "NotContains":
					return !valueToCompareLower.Contains(lowerFilterValue);
				case "GreaterThan":
				case "LessThan":
					if (!numericFilterValue.HasValue)
						return false; // Filter value couldn't be parsed as number
					object? rawPropertyValue = GetPropertyValue(entry, criterion.SelectedField);
					if (rawPropertyValue == null)
						return false;
					double entryNumericValueActual;
					if (rawPropertyValue is int intVal)
						entryNumericValueActual = intVal;
					else if (rawPropertyValue is long longVal)
						entryNumericValueActual = longVal;
					else if (rawPropertyValue != null && rawPropertyValue.GetType() == typeof(int?)) {
						var nullableInt = (int?)rawPropertyValue;
						if (nullableInt.HasValue)
							entryNumericValueActual = nullableInt.Value;
						else
							return false;
					}
					// Add other supported numeric types from IISLogEntry if GetPropertyValue can return them for numeric fields
					// else if (rawPropertyValue is short shortVal) entryNumericValueActual = shortVal; 
					// else if (rawPropertyValue is double doubleVal) entryNumericValueActual = doubleVal;
					// else if (rawPropertyValue is float floatVal) entryNumericValueActual = floatVal;
					else
						return false; // Property is not of an expected numeric type
					if (criterion.SelectedOperator == "GreaterThan")
						return entryNumericValueActual > numericFilterValue.Value;
					else // LessThan
						return entryNumericValueActual < numericFilterValue.Value;
				default:
					return false; // Default to false for unrecognized operators
			}
		}

		private object? GetPropertyValue(IisLogEntry entry, IISLogField field) {
			return field switch {
				IISLogField.Date => entry.DateTime.HasValue ? entry.DateTime.Value.Date : (DateTime?)null,
				IISLogField.Time => entry.DateTime.HasValue ? entry.DateTime.Value.TimeOfDay : (TimeSpan?)null,
				IISLogField.ServerIP => entry.ServerIPAddress,
				IISLogField.ClientIP => entry.ClientIPAddress,
				IISLogField.Method => entry.Method,
				IISLogField.UriStem => entry.UriStem,
				IISLogField.UriQuery => entry.UriQuery,
				IISLogField.Port => entry.ServerPort,
				IISLogField.UserName => entry.UserName,
				IISLogField.HttpStatus => entry.HttpStatus,
				IISLogField.Win32Status => entry.Win32Status,
				IISLogField.TimeTaken => entry.TimeTaken,
				IISLogField.UserAgent => entry.UserAgent,
				_ => null,
			};
		}

		private void ExecuteResetIISFilters() {
			IISFilterCriteria.Clear();
			IsIISErrorsOnly = false;
			FilteredIISLogEntries.Clear();
			foreach (var entry in IISLogEntries) {
				FilteredIISLogEntries.Add(entry);
			}
			ApplySortToIISEntries();
		}

		private void ExecuteSortIIS(string? columnName) {
			if (LogType != LogFormatType.IIS || string.IsNullOrEmpty(columnName))
				return;

			// Toggle sort direction if same column, otherwise set to ascending
			if (CurrentIISSortField == columnName) {
				IsIISAscending = !IsIISAscending;
			} else {
				CurrentIISSortField = columnName;
				IsIISAscending = true;
			}

			ApplySortToIISEntries();
		}

		private void ApplySortToIISEntries() {
			if (string.IsNullOrEmpty(CurrentIISSortField))
				return;

			var sortedEntries = CurrentIISSortField switch {
				"date" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.DateTime).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.DateTime).ToList(),
				"time" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.DateTime?.TimeOfDay).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.DateTime?.TimeOfDay).ToList(),
				"s-ip" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.ServerIPAddress).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.ServerIPAddress).ToList(),
				"cs-method" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.Method).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.Method).ToList(),
				"cs-uri-stem" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.UriStem).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.UriStem).ToList(),
				"cs-uri-query" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.UriQuery).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.UriQuery).ToList(),
				"s-port" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.ServerPort).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.ServerPort).ToList(),
				"cs-username" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.UserName).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.UserName).ToList(),
				"c-ip" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.ClientIPAddress).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.ClientIPAddress).ToList(),
				"time-taken" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.TimeTaken).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.TimeTaken).ToList(),
				"cs(User-Agent)" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.UserAgent).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.UserAgent).ToList(),
				"sc-win32-status" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.Win32Status).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.Win32Status).ToList(),
				"sc-status" => IsIISAscending 
					? FilteredIISLogEntries.OrderBy(e => e.HttpStatus).ToList()
					: FilteredIISLogEntries.OrderByDescending(e => e.HttpStatus).ToList(),
				_ => FilteredIISLogEntries.ToList()
			};

			FilteredIISLogEntries.Clear();
			foreach (var entry in sortedEntries) {
				FilteredIISLogEntries.Add(entry);
			}
		}

		private async void ExecuteExportIisLogsToCsv()
		{
			if (LogType != LogFormatType.IIS || !FilteredIISLogEntries.Any())
				return;

			try
			{
				// Simple file path generation for now - in production you might want to use Avalonia's file dialogs
				var fileName = $"IIS_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
				var filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
				
				var csv = new System.Text.StringBuilder();
				
				// Add header
				csv.AppendLine("Date,Time,Server IP,Method,URL Path,Query String,Port,User,Client IP,Time Taken (ms),User Agent,Win32 Status,HTTP Status");
				
				// Add data rows
				foreach (var entry in FilteredIISLogEntries)
				{
					var escapedUserAgent = EscapeCsvField(entry.ShortUserAgent);
					var escapedUriStem = EscapeCsvField(entry.UriStem);
					var escapedUriQuery = EscapeCsvField(entry.UriQuery);
					var escapedUserName = EscapeCsvField(entry.UserName);
					
					csv.AppendLine($"{entry.DateTime:yyyy-MM-dd},{entry.DateTime:HH:mm:ss},{entry.ServerIPAddress},{entry.Method},{escapedUriStem},{escapedUriQuery},{entry.ServerPort},{escapedUserName},{entry.ClientIPAddress},{entry.TimeTaken},{escapedUserAgent},{entry.Win32Status},{entry.HttpStatus}");
				}
				
				await System.IO.File.WriteAllTextAsync(filePath, csv.ToString());
				
				// For now, just write to console - in production you might want to show a toast notification
				Console.WriteLine($"Successfully exported {FilteredIISLogEntries.Count} log entries to {filePath}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error exporting CSV: {ex.Message}");
			}
		}

		private string EscapeCsvField(string? field)
		{
			if (string.IsNullOrEmpty(field))
				return "";
				
			// Escape quotes and wrap in quotes if necessary
			if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
			{
				return $"\"{field.Replace("\"", "\"\"")}\"";
			}
			
			return field;
		}

		// Standard Filter Methods

		private void ExecuteAddFilterCriterion() {
			if (LogType != LogFormatType.Standard)
				return;
			var newCriterion = new FilterCriterion { ParentViewModel = this };
			// Initialize AvailableFields from MasterAvailableFields
			foreach (var field in MasterAvailableFields) {
				newCriterion.AvailableFields.Add(field);
			}
			// Set initial SelectedField to trigger operators and values population
			if (MasterAvailableFields.Any()) {
				newCriterion.SelectedField = MasterAvailableFields.First();
			}
			FilterCriteria.Add(newCriterion);
		}

		private void ExecuteRemoveFilterCriterion(FilterCriterion? criterion) {
			if (LogType != LogFormatType.Standard)
				return;
			if (criterion != null) {
				FilterCriteria.Remove(criterion);
			}
		}

		private void ExecuteApplyFilters() {
			if (LogType != LogFormatType.Standard)
				return;
			List<LogEntry> tempList;
			if (FilterCriteria == null || !FilterCriteria.Any()) {
				tempList = new List<LogEntry>(LogEntries);
			} else {
				tempList = LogEntries.Where(entry => FilterCriteria.All(filter => MatchStandardFilter(entry, filter))).ToList();
			}
			FilteredLogEntries.Clear();
			foreach (var entry in tempList) {
				FilteredLogEntries.Add(entry);
			}
			OnPropertyChanged(nameof(FilteredLogEntries));
		}

		private void ExecuteResetFilters() {
			if (LogType != LogFormatType.Standard)
				return;
			FilterCriteria.Clear();
			FilteredLogEntries.Clear();
			foreach (var entry in LogEntries) {
				FilteredLogEntries.Add(entry);
			}
			OnPropertyChanged(nameof(FilteredLogEntries));
		}

		private bool MatchStandardFilter(LogEntry entry, FilterCriterion filter) {
			// Базовое сопоставление фильтров - может потребоваться доработка
			// в зависимости от структуры LogEntry и доступных полей/операторов
			string? value = GetLogEntryPropertyValue(entry, filter.SelectedField);
			if (value == null)
				return false;
			switch (filter.SelectedOperator) {
				case "Equals":
					return string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase);
				case "NotEquals":
					return !string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase);
				case "Contains":
					return value.Contains(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				case "NotContains":
					return !value.Contains(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				default:
					return false;
			}
		}

		private string? GetLogEntryPropertyValue(LogEntry entry, string? field) {
			if (field == null)
				return null;
			return field switch {
				"Message" => entry.Message,
				"Level" => entry.Level,
				"Timestamp" => entry.Timestamp.ToString(),
				"Source" => entry.Source,
				_ => null
			};
		}

		private void InitializeFilterFields() {
			MasterAvailableFields.Clear();
			OperatorsByFieldType.Clear();
			AvailableValuesByField.Clear();
			if (LogType == LogFormatType.Standard) {
				MasterAvailableFields.AddRange(new[] { "Timestamp", "Level", "Message", "Source" });
				OperatorsByFieldType["Timestamp"] = new List<string> { "Before", "After" };
				OperatorsByFieldType["Level"] = new List<string> { "Equals", "NotEquals" };
				OperatorsByFieldType["Message"] = new List<string> { "Contains", "NotContains" };
				OperatorsByFieldType["Source"] = new List<string> { "Equals", "NotEquals", "Contains" };
				AvailableValuesByField["Level"] = _logEntries.Select(l => l.Level).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
				AvailableValuesByField["Source"] = _logEntries.Select(l => l.Source).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList()!;
			} else if (LogType == LogFormatType.IIS) {
				// For IIS, the filter UI is different and populated directly in the IISFilterCriterion view model
				// We don't need to populate MasterAvailableFields for the standard filter UI
			} else if (LogType == LogFormatType.RabbitMQ) {
				// Initialize RabbitMQ filter fields
				MasterAvailableFields.AddRange(new[] { "Timestamp", "Level", "Message", "Node", "Username", "ProcessUID" });
				OperatorsByFieldType["Timestamp"] = new List<string> { "Before", "After", "Equals" };
				OperatorsByFieldType["Level"] = new List<string> { "Equals", "Not Equals" };
				OperatorsByFieldType["Message"] = new List<string> { "Contains", "Not Contains", "Equals", "StartsWith", "EndsWith" };
				OperatorsByFieldType["Node"] = new List<string> { "Equals", "Contains" };
				OperatorsByFieldType["Username"] = new List<string> { "Equals", "Not Equals", "Contains" };
				OperatorsByFieldType["ProcessUID"] = new List<string> { "Equals", "Not Equals", "Contains" };
				
				// Populate available values from RabbitMQ entries
				AvailableValuesByField["Level"] = _rabbitMqLogEntries.Select(l => l.EffectiveLevel).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList()!;
				AvailableValuesByField["Node"] = _rabbitMqLogEntries.Select(l => l.EffectiveNode).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList()!;
				AvailableValuesByField["Username"] = _rabbitMqLogEntries.Select(l => l.EffectiveUserName).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList()!;
				AvailableValuesByField["ProcessUID"] = _rabbitMqLogEntries.Select(l => l.EffectiveProcessUID).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList()!;
			}
		}

		// RabbitMQ Filter Methods

		private void ExecuteAddRabbitMQFilterCriterion() {
			if (LogType != LogFormatType.RabbitMQ)
				return;
			var newCriterion = new FilterCriterion { ParentViewModel = this };
			// Initialize AvailableFields from MasterAvailableFields
			foreach (var field in MasterAvailableFields) {
				newCriterion.AvailableFields.Add(field);
			}
			// Set initial SelectedField to trigger operators and values population
			if (MasterAvailableFields.Any()) {
				newCriterion.SelectedField = MasterAvailableFields.First();
			}
			RabbitMQFilterCriteria.Add(newCriterion);
		}

		private void ExecuteRemoveRabbitMQFilterCriterion(FilterCriterion? criterion) {
			if (LogType != LogFormatType.RabbitMQ)
				return;
			if (criterion != null) {
				RabbitMQFilterCriteria.Remove(criterion);
			}
		}

		private async void ExecuteApplyRabbitMQFilters() {
			if (LogType != LogFormatType.RabbitMQ)
				return;

			try {
				var rabbitMqFilterService = App.ServiceProvider?.GetService(typeof(IRabbitMQFilterService)) as IRabbitMQFilterService;
				if (rabbitMqFilterService == null) {
					// Fallback to manual filtering if service not available
					ApplyRabbitMQFiltersManually();
					return;
				}

				var filteredEntries = await rabbitMqFilterService.ApplySimpleFiltersAsync(RabbitMQLogEntries, RabbitMQFilterCriteria);
				
				FilteredRabbitMQLogEntries.Clear();
				foreach (var entry in filteredEntries) {
					FilteredRabbitMQLogEntries.Add(entry);
				}

				// Update counts
				OnPropertyChanged(nameof(RabbitMQ_TotalCount));
				OnPropertyChanged(nameof(RabbitMQ_ErrorCount));
				OnPropertyChanged(nameof(RabbitMQ_WarningCount));
				OnPropertyChanged(nameof(RabbitMQ_InfoCount));
			}
			catch (Exception ex) {
				// Log error and fallback to manual filtering
				System.Diagnostics.Debug.WriteLine($"Error applying RabbitMQ filters: {ex.Message}");
				ApplyRabbitMQFiltersManually();
			}
		}

		private void ApplyRabbitMQFiltersManually() {
			List<RabbitMqLogEntry> tempList;
			if (RabbitMQFilterCriteria == null || !RabbitMQFilterCriteria.Any()) {
				tempList = new List<RabbitMqLogEntry>(RabbitMQLogEntries);
			} else {
				tempList = RabbitMQLogEntries.Where(entry => RabbitMQFilterCriteria.All(filter => MatchRabbitMQFilter(entry, filter))).ToList();
			}
			FilteredRabbitMQLogEntries.Clear();
			foreach (var entry in tempList) {
				FilteredRabbitMQLogEntries.Add(entry);
			}
			
			// Update counts
			OnPropertyChanged(nameof(RabbitMQ_TotalCount));
			OnPropertyChanged(nameof(RabbitMQ_ErrorCount));
			OnPropertyChanged(nameof(RabbitMQ_WarningCount));
			OnPropertyChanged(nameof(RabbitMQ_InfoCount));
		}

		private void ExecuteResetRabbitMQFilters() {
			if (LogType != LogFormatType.RabbitMQ)
				return;
			RabbitMQFilterCriteria.Clear();
			FilteredRabbitMQLogEntries.Clear();
			foreach (var entry in RabbitMQLogEntries) {
				FilteredRabbitMQLogEntries.Add(entry);
			}
			
			// Update counts
			OnPropertyChanged(nameof(RabbitMQ_TotalCount));
			OnPropertyChanged(nameof(RabbitMQ_ErrorCount));
			OnPropertyChanged(nameof(RabbitMQ_WarningCount));
			OnPropertyChanged(nameof(RabbitMQ_InfoCount));
		}

		private bool MatchRabbitMQFilter(RabbitMqLogEntry entry, FilterCriterion filter) {
			string? value = GetRabbitMQLogEntryPropertyValue(entry, filter.SelectedField);
			if (value == null && !string.IsNullOrEmpty(filter.Value))
				return false;
			if (value == null)
				return true;

			switch (filter.SelectedOperator) {
				case "Equals":
					return string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase);
				case "Not Equals":
					return !string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase);
				case "Contains":
					return value.Contains(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				case "Not Contains":
					return !value.Contains(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				case "StartsWith":
					return value.StartsWith(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				case "EndsWith":
					return value.EndsWith(filter.Value ?? "", StringComparison.OrdinalIgnoreCase);
				case "Before":
					if (DateTime.TryParse(filter.Value, out var beforeDate) && DateTime.TryParse(value, out var entryDate))
						return entryDate < beforeDate;
					return false;
				case "After":
					if (DateTime.TryParse(filter.Value, out var afterDate) && DateTime.TryParse(value, out var entryDate2))
						return entryDate2 > afterDate;
					return false;
				default:
					return false;
			}
		}

		private string? GetRabbitMQLogEntryPropertyValue(RabbitMqLogEntry entry, string? field) {
			if (field == null)
				return null;
			return field switch {
				"Message" => entry.EffectiveMessage,
				"Level" => entry.EffectiveLevel,
				"Timestamp" => entry.EffectiveTimestamp.ToString(),
				"Node" => entry.EffectiveNode,
				"Username" => entry.EffectiveUserName,
				"ProcessUID" => entry.EffectiveProcessUID,
				_ => null
			};
		}

		#endregion

		// Methods for IISFilterCriterion to get operators and distinct values

		#region Methods: Public

		public IEnumerable<string> GetOperatorsForIISField(IISLogField field) {
			var operators = new List<string> { "Equals", "NotEquals", "Contains", "NotContains" };
			// Add numeric operators for specific fields
			if (field == IISLogField.TimeTaken || field == IISLogField.Port || field == IISLogField.HttpStatus || field == IISLogField.Win32Status) {
				operators.Add("GreaterThan");
				operators.Add("LessThan");
			}
			// Potentially add "StartsWith", "EndsWith" for string fields
			// Potentially add "IsNull", "IsNotNull"
			return operators;
		}

		public IEnumerable<string> GetDistinctValuesForIISField(IISLogField field) {
			if (IISLogEntries == null || !IISLogEntries.Any()) {
				return Enumerable.Empty<string>();
			}
			// Return predefined lists for some fields for better UX
			if (field == IISLogField.HttpStatus) {
				return new List<string> { "200", "201", "204", "301", "302", "304", "400", "401", "403", "404", "500", "502", "503" }.OrderBy(s => s).ToList();
			}
			if (field == IISLogField.Method) {
				return new List<string> { "GET", "POST", "PUT", "DELETE", "HEAD", "OPTIONS", "PATCH", "TRACE", "CONNECT" }.OrderBy(s => s).ToList();
			}
			// For other fields, get distinct values from the log entries
			var distinctValues = IISLogEntries.Select(entry => GetPropertyValue(entry, field)?.ToString()).Where(value => !string.IsNullOrEmpty(value))
				.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
			return distinctValues!; // Non-null asserted because of Where clause
		}

		#endregion

	}

	#endregion

}
