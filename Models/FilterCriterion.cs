using System.Linq;

namespace Log_Parser_App.Models
{
    using System.Collections.Generic;
    using Log_Parser_App.ViewModels;


    public class FilterCriterion : ViewModelBase
    {
        private string? _selectedField;
        public string? SelectedField {
            get => _selectedField;
            set {
                if (!SetProperty(ref _selectedField, value))
                    return;

                SelectedOperator = null;
                Value = string.Empty;
                OnPropertyChanged(nameof(AvailableOperators));
                OnPropertyChanged(nameof(AvailableValues));
                OnPropertyChanged(nameof(ShowValueComboBox));
            }
        }

        private string? _selectedOperator;
        public string? SelectedOperator {
            get => _selectedOperator;
            set => SetProperty(ref _selectedOperator, value);
        }

        private string? _value = string.Empty;
        public string? Value {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public MainWindowViewModel? ParentViewModel { get; set; }

        public IEnumerable<string> AvailableOperators =>
            ParentViewModel?.OperatorsByFieldType.TryGetValue(SelectedField, out var ops) == true
                ? ops
                : Enumerable.Empty<string>();

        public List<string>? AvailableFields => ParentViewModel?.AvailableFields;

        public List<string>? AvailableValues =>
            SelectedField != null && ParentViewModel?.AvailableValuesByField.ContainsKey(SelectedField) == true
                ? new List<string>(ParentViewModel.AvailableValuesByField[SelectedField])
                : null;

        public bool ShowValueComboBox =>
            SelectedField != null && ParentViewModel?.AvailableValuesByField.ContainsKey(SelectedField) == true && ParentViewModel.AvailableValuesByField[SelectedField].Count > 0;
    }
}