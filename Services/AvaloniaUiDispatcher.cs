using System;
using Avalonia.Threading;
using Log_Parser_App.Services.Interfaces;

namespace Log_Parser_App.Services;

public class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Invoke(Action action)
    {
        Dispatcher.UIThread.Invoke(action);
    }
}

