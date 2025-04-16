using System.Collections.Generic;
using LogParserApp.ViewModels; // Assuming ViewModelBase is here or adjust namespace

namespace LogParserApp.Models
{
    public class FilterCriterion : ViewModelBase // Inherit from ViewModelBase for INotifyPropertyChanged if needed
    {
        private string? _selectedField;
        public string? SelectedField
        {
            get => _selectedField;
            set
            {
                if (SetProperty(ref _selectedField, value))
                {
                    // Reset operator and value when field changes
                    SelectedOperator = null;
                    Value = string.Empty;
                    // Optionally, trigger update for available operators if needed
                    OnPropertyChanged(nameof(AvailableOperators)); 
                }
            }
        }

        private string? _selectedOperator;
        public string? SelectedOperator
        {
            get => _selectedOperator;
            set => SetProperty(ref _selectedOperator, value);
        }

        private string? _value = string.Empty;
        public string? Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        
        // Reference to the parent ViewModel to access lists and commands
        public MainWindowViewModel? ParentViewModel { get; set; }

        // Property to get available operators based on selected field from ParentViewModel
        public List<string>? AvailableOperators => 
            SelectedField != null && ParentViewModel?.OperatorsByFieldType.ContainsKey(SelectedField) == true
            ? ParentViewModel.OperatorsByFieldType[SelectedField]
            : null;

        // Property to get available fields from ParentViewModel
        public List<string>? AvailableFields => ParentViewModel?.AvailableFields;
    }
} 