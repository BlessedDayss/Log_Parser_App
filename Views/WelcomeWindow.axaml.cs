namespace Log_Parser_App.Views
{
    using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Media;

    public partial class WelcomeWindow : Window
    {
        #region Fields: Private

        private readonly TextBlock? _versionTextBlock;
        private readonly Button? _getStartedButton;
        private readonly Border? _featureCard1;
        private readonly Border? _featureCard2;
        private readonly Border? _featureCard3;

        #endregion

        #region Events

        public event EventHandler? GetStartedClicked;

        #endregion

        #region Constructors: Public

        public WelcomeWindow()
        {
            InitializeComponent();

            _versionTextBlock = this.FindControl<TextBlock>("VersionTextBlock");
            _getStartedButton = this.FindControl<Button>("GetStartedButton");
            _featureCard1 = this.FindControl<Border>("FeatureCard1");
            _featureCard2 = this.FindControl<Border>("FeatureCard2");
            _featureCard3 = this.FindControl<Border>("FeatureCard3");

            SetVersionNumber();
            SetupHoverEffects();
        }

        #endregion

        #region Methods: Private

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetVersionNumber()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null && _versionTextBlock != null)
            {
                _versionTextBlock.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
            }
        }

        private void SetupHoverEffects()
        {
            // Button hover effects (no scale to avoid clipping)
            if (_getStartedButton != null)
            {
                _getStartedButton.PointerEntered += (s, e) =>
                {
                    _getStartedButton.Background = new SolidColorBrush(Color.Parse("#4CAF50"));
                    _getStartedButton.Foreground = new SolidColorBrush(Color.Parse("#F5F5F7"));
                };
                _getStartedButton.PointerExited += (s, e) =>
                {
                    _getStartedButton.Background = new SolidColorBrush(Color.Parse("#2A2D32"));
                    _getStartedButton.Foreground = new SolidColorBrush(Color.Parse("#F5F5F7"));
                };
            }

            // Feature card hover effects
            if (_featureCard1 != null)
            {
                _featureCard1.PointerEntered += FeatureCard_PointerEntered;
                _featureCard1.PointerExited += FeatureCard_PointerExited;
            }

            if (_featureCard2 != null)
            {
                _featureCard2.PointerEntered += FeatureCard_PointerEntered;
                _featureCard2.PointerExited += FeatureCard_PointerExited;
            }

            if (_featureCard3 != null)
            {
                _featureCard3.PointerEntered += FeatureCard_PointerEntered;
                _featureCard3.PointerExited += FeatureCard_PointerExited;
            }
        }

        private void GetStartedButton_OnClick(object? sender, RoutedEventArgs e)
        {
            GetStartedClicked?.Invoke(this, EventArgs.Empty);
            // Don't close here - let the main app handle the transition
        }

        private void GetStartedButton_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_getStartedButton != null)
            {
                _getStartedButton.Background = new SolidColorBrush(Color.Parse("#4CAF50"));
                _getStartedButton.Foreground = new SolidColorBrush(Color.Parse("#F5F5F7"));
                _getStartedButton.RenderTransform = new ScaleTransform(1.02, 1.02);
            }
        }

        private void GetStartedButton_PointerExited(object? sender, PointerEventArgs e)
        {
            if (_getStartedButton != null)
            {
                _getStartedButton.Background = new SolidColorBrush(Color.Parse("#2A2D32"));
                _getStartedButton.Foreground = new SolidColorBrush(Color.Parse("#F5F5F7"));
                _getStartedButton.RenderTransform = new ScaleTransform(1.0, 1.0);
            }
        }

        private void FeatureCard_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Border card)
            {
                card.Opacity = 1.0;
            }
        }

        private void FeatureCard_PointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Border card)
            {
                card.Opacity = 0.9;
            }
        }

        #endregion
    }
}
