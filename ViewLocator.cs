namespace Log_Parser_App
{
	using System;
	using Avalonia.Controls;
	using Avalonia.Controls.Templates;
	using Log_Parser_App.ViewModels;

	#region Class: ViewLocator

	public class ViewLocator : IDataTemplate
	{

		#region Methods: Public

		public Control? Build(object? param) {
			if (param is null)
				return null;
			string name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
			var type = Type.GetType(name);
			if (type != null) {
				return (Control)Activator.CreateInstance(type)!;
			}
			return new TextBlock { Text = "Not Found: " + name };
		}

		public bool Match(object? data) {
			return data is ViewModelBase;
		}

		#endregion

	}

	#endregion

}