namespace Log_Parser_App.Views
{
	using System.Reflection;
	using Avalonia.Controls;
	using Avalonia.Markup.Xaml;

	#region Class: SplashScreen

	public partial class SplashScreen : Window
	{

		#region Fields: Private

		private readonly TextBlock? _statusTextBlock;
		private readonly TextBlock? _versionTextBlock;

		#endregion

		#region Constructors: Public

		public SplashScreen() {
			InitializeComponent();
			_statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
			_versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
			SetVersionNumber();
		}

		#endregion

		#region Methods: Private

		private void InitializeComponent() {
			AvaloniaXamlLoader.Load(this);
		}

		private void SetVersionNumber() {
			var version = Assembly.GetExecutingAssembly().GetName().Version;
			if (version != null && _versionTextBlock != null) {
				_versionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
			}
		}

		#endregion

		#region Methods: Public

		public void UpdateStatus(string status) {
			if (_statusTextBlock != null) {
				_statusTextBlock.Text = status;
			}
		}

		#endregion

	}

	#endregion

}
