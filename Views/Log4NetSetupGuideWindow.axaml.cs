using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace Log_Parser_App.Views
{
    public partial class Log4NetSetupGuideWindow : Window
    {
        public bool DontShowAgain { get; private set; }
        public bool OpenSettings { get; private set; }

        public Log4NetSetupGuideWindow()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            var openSettingsButton = this.FindControl<Button>("OpenSettingsButton");
            var closeButton = this.FindControl<Button>("CloseButton");
            var dontShowAgainCheckBox = this.FindControl<CheckBox>("DontShowAgainCheckBox");

            if (openSettingsButton != null)
                openSettingsButton.Click += OpenSettingsButton_Click;
            
            if (closeButton != null)
                closeButton.Click += CloseButton_Click;
                
            if (dontShowAgainCheckBox != null)
                dontShowAgainCheckBox.IsCheckedChanged += DontShowAgainCheckBox_CheckedChanged;
        }

        private void OpenSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            OpenSettings = true;
            
            var dontShowAgainCheckBox = this.FindControl<CheckBox>("DontShowAgainCheckBox");
            DontShowAgain = dontShowAgainCheckBox?.IsChecked == true;
            
            Close();
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            OpenSettings = false;
            
            var dontShowAgainCheckBox = this.FindControl<CheckBox>("DontShowAgainCheckBox");
            DontShowAgain = dontShowAgainCheckBox?.IsChecked == true;
            
            Close();
        }

        private void DontShowAgainCheckBox_CheckedChanged(object? sender, RoutedEventArgs e)
        {
            // Event handler for checkbox state change if needed
        }
    }
}


