namespace Log_Parser_App.Models
{
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

	#region Class: FilterCriterion

	public partial class FilterCriterion : ObservableObject
	{

		#region Fields: Private

		[ObservableProperty]
		private bool _isActive = true;
		[ObservableProperty]
		private string? _selectedField;
		[ObservableProperty]
		private string? _selectedOperator;
		[ObservableProperty]
		private string? _value;

		#endregion

		#region Properties: Public

		public TabViewModel? ParentViewModel { get; set; }

		public ObservableCollection<string> AvailableFields { get; } = new();

		public ObservableCollection<string> AvailableOperators { get; } = new();

		public ObservableCollection<string> AvailableValues { get; } = new();

		public bool ShowValueComboBox => AvailableValues.Any();

		#endregion

		#region Methods: Private

		partial void OnSelectedFieldChanged(string? value) {
			UpdateAvailableOperators();
			UpdateAvailableValues();
		}

		private void UpdateAvailableOperators() {
			AvailableOperators.Clear();
			if (ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.OperatorsByFieldType.ContainsKey(SelectedField)) {
				foreach (string op in ParentViewModel.OperatorsByFieldType[SelectedField]) {
					AvailableOperators.Add(op);
				}
				// Only set default if we actually have operators
				SelectedOperator = AvailableOperators.FirstOrDefault();
			}
		}

		private void UpdateAvailableValues() {
			AvailableValues.Clear();
			if (ParentViewModel != null && !string.IsNullOrEmpty(SelectedField) && ParentViewModel.AvailableValuesByField.ContainsKey(SelectedField)) {
				foreach (string val in ParentViewModel.AvailableValuesByField[SelectedField]) {
					AvailableValues.Add(val);
				}
			}
			OnPropertyChanged(nameof(ShowValueComboBox));
		}

		#endregion

		#region Events: Public

		public new event PropertyChangedEventHandler? PropertyChanged;

		#endregion

		#region Methods: Protected

		protected new bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null) {
			if (EqualityComparer<T>.Default.Equals(storage, value)) {
				return false;
			}
			storage = value;
			OnPropertyChanged(propertyName);
			return true;
		}

		protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			if (propertyName == nameof(SelectedField) || propertyName == nameof(ParentViewModel)) {
				OnPropertyChanged(nameof(ShowValueComboBox));
			}
		}

		#endregion

	}

	#endregion

}
