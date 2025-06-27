namespace Log_Parser_App.Models
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using Log_Parser_App.ViewModels;
    using System.Linq;
    using System.Runtime.CompilerServices;


    public enum IISLogField
    {
        Date,
        Time,
        ServerIP,
        ClientIP,
        Method,
        UriStem,
        UriQuery,
        Port,
        UserName,
        HttpStatus,
        Win32Status,
        TimeTaken,
        UserAgent,
    }

    public class IISFilterCriterion : INotifyPropertyChanged
    {
        private IISLogField _selectedField;
        private string _selectedOperator = string.Empty;
        private string _value = string.Empty;
        private string _manualValue = string.Empty;
        private bool _useManualInput = false;
        private string _logicalOperator = "AND"; // Default to AND

        [System.Text.Json.Serialization.JsonIgnore]
        public TabViewModel? ParentViewModel { get; set; } // Changed from dynamic?

        public ObservableCollection<IISLogField> AvailableFields { get; }
        public ObservableCollection<string> AvailableOperators { get; } = new();
        public ObservableCollection<string> AvailableValues { get; } = new();
        public List<string> AvailableLogicalOperators { get; } = new() { "AND", "OR" };

        public IISFilterCriterion() {
            AvailableFields = new ObservableCollection<IISLogField>((IISLogField[])System.Enum.GetValues(typeof(IISLogField)));
            _selectedField = AvailableFields.FirstOrDefault();
            // Operators and Values will be updated once ParentViewModel is set and SelectedField changes.
        }

        public IISLogField SelectedField {
            get => _selectedField;
            set {
                if (SetProperty(ref _selectedField, value)) {
                    UpdateAvailableOperators();
                    UpdateAvailableValues();
                }
            }
        }

        public string SelectedOperator {
            get => _selectedOperator;
            set => SetProperty(ref _selectedOperator, value);
        }

        public string Value {
            get => _useManualInput ? _manualValue : _value;
            set {
                if (_useManualInput) {
                    SetProperty(ref _manualValue, value);
                } else {
                    SetProperty(ref _value, value);
                }
            }
        }

        public string ManualValue {
            get => _manualValue;
            set {
                if (SetProperty(ref _manualValue, value)) {
                    if (_useManualInput) {
                        OnPropertyChanged(nameof(Value));
                    }
                }
            }
        }

        public bool UseManualInput {
            get => _useManualInput;
            set {
                if (SetProperty(ref _useManualInput, value)) {
                    OnPropertyChanged(nameof(Value));
                    OnPropertyChanged(nameof(ShowValueComboBox));
                    OnPropertyChanged(nameof(ShowTextBox));
                }
            }
        }

        public string LogicalOperator {
            get => _logicalOperator;
            set => SetProperty(ref _logicalOperator, value);
        }

        // Compatibility properties for services
        public string Field => SelectedField.ToString();
        public string Operator => SelectedOperator;
        public bool IsEnabled { get; set; } = true;

        public bool ShowValueComboBox {
            get {
                // Show ComboBox if not using manual input and ParentViewModel provides distinct values
                return !_useManualInput && ParentViewModel != null && ParentViewModel.GetDistinctValuesForIISField(SelectedField).Any();
            }
        }

        public bool ShowTextBox {
            get {
                // Show TextBox if using manual input OR if no predefined values available
                return _useManualInput || (ParentViewModel != null && !ParentViewModel.GetDistinctValuesForIISField(SelectedField).Any());
            }
        }

        private void UpdateAvailableOperators() {
            AvailableOperators.Clear();
            if (ParentViewModel != null) {
                var operators = ParentViewModel.GetOperatorsForIISField(SelectedField);
                foreach (var op in operators) {
                    AvailableOperators.Add(op);
                }
            }

            if (!AvailableOperators.Contains(SelectedOperator)) {
                SelectedOperator = AvailableOperators.FirstOrDefault() ?? string.Empty;
            }
            // Ensure PropertyChanged is raised for SelectedOperator if it's changed programmatically
            OnPropertyChanged(nameof(SelectedOperator));
        }

        private void UpdateAvailableValues() {
            AvailableValues.Clear();
            bool hasPredefindValues = ParentViewModel != null && ParentViewModel.GetDistinctValuesForIISField(SelectedField).Any();

            if (hasPredefindValues && ParentViewModel != null) 
            {
                var values = ParentViewModel.GetDistinctValuesForIISField(SelectedField);
                foreach (var val in values) {
                    AvailableValues.Add(val);
                }
            }

            if (!hasPredefindValues || !AvailableValues.Contains(_value)) 
            {
                _value = string.Empty;
            }
            
            // Reset manual input if switching to a field with predefined values
            if (hasPredefindValues && !_useManualInput) {
                _manualValue = string.Empty;
            }
            
            // Ensure PropertyChanged is raised for Value if it's changed programmatically
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ShowValueComboBox));
            OnPropertyChanged(nameof(ShowTextBox));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
            if (EqualityComparer<T>.Default.Equals(storage, value)) {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(SelectedField) || propertyName == nameof(ParentViewModel)) {
                // When SelectedField or ParentViewModel changes, we need to re-evaluate ShowValueComboBox
                // and potentially update operators and values.
                UpdateAvailableOperators(); // Added to ensure operators update if ParentViewModel changes after field selection
                UpdateAvailableValues(); // Added to ensure values update if ParentViewModel changes after field selection
                OnPropertyChanged(nameof(ShowValueComboBox));
                OnPropertyChanged(nameof(ShowTextBox));
            }
        }
    }
}