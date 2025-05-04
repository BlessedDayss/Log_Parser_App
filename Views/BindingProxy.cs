namespace Log_Parser_App.Views
{
    using Avalonia;


    public class BindingProxy : AvaloniaObject
    {
        private static readonly StyledProperty<object?> DataProperty = AvaloniaProperty.Register<BindingProxy, object?>(nameof(Data));

        public object? Data {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }
    }
}