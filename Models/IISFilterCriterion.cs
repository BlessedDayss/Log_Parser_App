using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Log_Parser_App.ViewModels; // Потенциально понадобится FileTabViewModel или MainViewModel

namespace Log_Parser_App.Models
{
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
        Referer
        // Можно добавить другие поля при необходимости
    }

    public class IISFilterCriterion : INotifyPropertyChanged
    {
        private IISLogField _selectedField;
        private string _selectedOperator = string.Empty;
        private string _value = string.Empty;
        
        [System.Text.Json.Serialization.JsonIgnore]
        public TabViewModel? ParentViewModel { get; set; } // Changed from dynamic?

        public ObservableCollection<IISLogField> AvailableFields { get; }
        public ObservableCollection<string> AvailableOperators { get; } = new();
        public ObservableCollection<string> AvailableValues { get; } = new();

        public IISFilterCriterion()
        {
            AvailableFields = new ObservableCollection<IISLogField>((IISLogField[])System.Enum.GetValues(typeof(IISLogField)));
            _selectedField = AvailableFields.FirstOrDefault();
            // Operators and Values will be updated once ParentViewModel is set and SelectedField changes.
        }

        public IISLogField SelectedField
        {
            get => _selectedField;
            set
            {
                if (SetProperty(ref _selectedField, value))
                {
                    UpdateAvailableOperators();
                    UpdateAvailableValues(); 
                }
            }
        }

        public string SelectedOperator
        {
            get => _selectedOperator;
            set => SetProperty(ref _selectedOperator, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public bool ShowValueComboBox
        {
            get
            {
                // Show ComboBox if ParentViewModel is set and it provides any distinct values for the current field.
                return ParentViewModel != null && ParentViewModel.GetDistinctValuesForIISField(SelectedField).Any();
            }
        }

        private void UpdateAvailableOperators()
        {
            AvailableOperators.Clear();
            if (ParentViewModel != null)
            {
                var operators = ParentViewModel.GetOperatorsForIISField(SelectedField);
                foreach (var op in operators)
                {
                    AvailableOperators.Add(op);
                }
            }
            
            if (!AvailableOperators.Contains(SelectedOperator))
            {
                SelectedOperator = AvailableOperators.FirstOrDefault() ?? string.Empty;
            }
            // Ensure PropertyChanged is raised for SelectedOperator if it's changed programmatically
            OnPropertyChanged(nameof(SelectedOperator)); 
        }

        private void UpdateAvailableValues()
        {
            AvailableValues.Clear();
            bool shouldShowComboBox = ShowValueComboBox; // Cache this value

            if (shouldShowComboBox && ParentViewModel != null) // Use cached value
            {
                var values = ParentViewModel.GetDistinctValuesForIISField(SelectedField);
                foreach (var val in values)
                {
                   AvailableValues.Add(val);
                }
            }

            if (!shouldShowComboBox || !AvailableValues.Contains(Value)) // Use cached value
            {
                Value = string.Empty; 
            }
            // Ensure PropertyChanged is raised for Value if it's changed programmatically
            OnPropertyChanged(nameof(Value)); 
            OnPropertyChanged(nameof(ShowValueComboBox)); // This needs to be explicitly notified if its conditions change
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(SelectedField) || propertyName == nameof(ParentViewModel))
            {
                // When SelectedField or ParentViewModel changes, we need to re-evaluate ShowValueComboBox
                // and potentially update operators and values.
                UpdateAvailableOperators(); // Added to ensure operators update if ParentViewModel changes after field selection
                UpdateAvailableValues();    // Added to ensure values update if ParentViewModel changes after field selection
                OnPropertyChanged(nameof(ShowValueComboBox));
            }
        }
    }
} 