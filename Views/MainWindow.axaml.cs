using Avalonia.Controls;
using LogParserApp.ViewModels;
using Avalonia;
using Avalonia.Styling;

namespace LogParserApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        this.AttachedToVisualTree += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm && vm.MainView != null)
            {
                UpdateTheme(vm.MainView.IsDarkTheme);
                
                vm.MainView.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(vm.MainView.IsDarkTheme))
                    {
                        UpdateTheme(vm.MainView.IsDarkTheme);
                    }
                };
            }
        };
    }
    
    private void UpdateTheme(bool isDarkTheme)
    {
        Application.Current!.RequestedThemeVariant = 
            isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
    }
}