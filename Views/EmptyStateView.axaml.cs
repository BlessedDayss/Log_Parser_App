namespace Log_Parser_App.Views
{
    using Avalonia.Controls;
    using Avalonia.Markup.Xaml;
    using System;

    public partial class EmptyStateView : UserControl
    {
        public event EventHandler? OpenLogFileRequested;

        public EmptyStateView() {
            InitializeComponent();

            var openLogFileButton = this.FindControl<Button>("OpenLogFileButton");
            if (openLogFileButton != null) {
                openLogFileButton.Click += (s, e) => OpenLogFileRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void InitializeComponent() {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
