using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Log_Parser_App.Views
{
    public partial class SplashScreen : Window
    {
        private readonly TextBlock? _statusTextBlock;
        private readonly TextBlock? _versionTextBlock;

        public SplashScreen()
        {
            InitializeComponent();
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
            _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
            SetVersionNumber();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public void UpdateStatus(string status)
        {
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Text = status;
            }
        }

        private void SetVersionNumber()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null && _versionTextBlock != null)
            {
                _versionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }
    }
} 