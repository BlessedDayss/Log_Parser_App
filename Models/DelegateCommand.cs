namespace Log_Parser_App.Models
{
    using System;
    using System.Windows.Input;


    public class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
    {

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => execute();
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}