using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Log_Parser_App.ViewModels;
using System.Linq;

namespace Log_Parser_App.Models
{
    public class FilterCriterion : INotifyPropertyChanged
    {
        private string _selectedField = string.Empty;
        private string _selectedOperator = string.Empty;
        private string _value = string.Empty;
        private bool _showValueComboBox;

        public ObservableCollection<string> AvailableFields { get; set; } = new();
        public ObservableCollection<string> AvailableOperators { get; set; } = new();
        public ObservableCollection<string> AvailableValues { get; set; } = new();
        
        [System.Text.Json.Serialization.JsonIgnore]
        public TabViewModel? ParentViewModel { get; set; }

        public string SelectedField
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
        
        public bool ShowValueComboBox =>
            ParentViewModel != null &&
            !string.IsNullOrEmpty(SelectedField) &&
            ParentViewModel.AvailableValuesByField.ContainsKey(SelectedField) &&
            ParentViewModel.AvailableValuesByField[SelectedField].Any();

        private void UpdateAvailableOperators()
        {
            if (ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.OperatorsByFieldType.ContainsKey(SelectedField))
            {
                AvailableOperators.Clear();
                foreach (var op in ParentViewModel.OperatorsByFieldType[SelectedField])
                {
                    AvailableOperators.Add(op);
                }
            }
            else
            {
                AvailableOperators.Clear();
                // Добавляем стандартные операторы
                AvailableOperators.Add("Equals");
                AvailableOperators.Add("NotEquals");
                AvailableOperators.Add("Contains");
                AvailableOperators.Add("NotContains");
            }
            if (!AvailableOperators.Contains(SelectedOperator)) 
            {
                SelectedOperator = AvailableOperators.FirstOrDefault() ?? string.Empty;
            }
        }

        private void UpdateAvailableValues()
        {
            if (ShowValueComboBox && ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.AvailableValuesByField.ContainsKey(SelectedField))
            {
                AvailableValues.Clear();
                foreach (var val in ParentViewModel.AvailableValuesByField[SelectedField])
                {
                    AvailableValues.Add(val);
                }
            }
            else
            {
                AvailableValues.Clear();
            }
            if (!ShowValueComboBox || !AvailableValues.Contains(Value)) 
            {
                Value = string.Empty;
            }
            OnPropertyChanged(nameof(ShowValueComboBox));
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
                OnPropertyChanged(nameof(ShowValueComboBox));
            }
        }
    }
} 