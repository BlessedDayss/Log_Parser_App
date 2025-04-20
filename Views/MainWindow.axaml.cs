using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using LogParserApp.ViewModels;

namespace Log_Parser_App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        this.AttachedToVisualTree += (_, _) =>
        {
            if (DataContext is MainWindowViewModel { MainView: not null } vm)
            {
                UpdateTheme(vm.MainView.IsDarkTheme);
                
                vm.MainView.PropertyChanged += (_, args) =>
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