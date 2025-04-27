using Avalonia;

namespace Log_Parser_App.Views
{
    public class BindingProxy : AvaloniaObject
    {
        public static readonly StyledProperty<object?> DataProperty =
            AvaloniaProperty.Register<BindingProxy, object?>(nameof(Data));

        public object? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
    }
} 