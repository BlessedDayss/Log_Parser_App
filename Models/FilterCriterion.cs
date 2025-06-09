using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Log_Parser_App.ViewModels;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Log_Parser_App.Models
{
    public partial class FilterCriterion : ObservableObject
    {
        [ObservableProperty]
        private bool _isActive = true;

        [ObservableProperty]
        private string? _selectedField;

        [ObservableProperty]
        private string? _selectedOperator;

        [ObservableProperty]
        private string? _value;

        public TabViewModel? ParentViewModel { get; set; }

        public ObservableCollection<string> AvailableFields { get; } = new();
        public ObservableCollection<string> AvailableOperators { get; } = new();
        public ObservableCollection<string> AvailableValues { get; } = new();

        public bool ShowValueComboBox => AvailableValues.Any();

        partial void OnSelectedFieldChanged(string? value)
        {
            UpdateAvailableOperators();
            UpdateAvailableValues();
        }

        private void UpdateAvailableOperators()
        {
            AvailableOperators.Clear();
            if (ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.OperatorsByFieldType.ContainsKey(SelectedField))
            {
                foreach (var op in ParentViewModel.OperatorsByFieldType[SelectedField])
                {
                    AvailableOperators.Add(op);
                }
            }
            SelectedOperator = AvailableOperators.FirstOrDefault();
        }

        private void UpdateAvailableValues()
        {
            AvailableValues.Clear();
            if (ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.AvailableValuesByField.ContainsKey(SelectedField))
            {
                foreach (var val in ParentViewModel.AvailableValuesByField[SelectedField])
                {
                    AvailableValues.Add(val);
                }
            }
            OnPropertyChanged(nameof(ShowValueComboBox));
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(SelectedField) || propertyName == nameof(ParentViewModel))
            {
                OnPropertyChanged(nameof(ShowValueComboBox));
            }
        }
    }
} 