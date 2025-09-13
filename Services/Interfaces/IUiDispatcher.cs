using System;

namespace Log_Parser_App.Services.Interfaces;

public interface IUiDispatcher
{
    void Invoke(Action action);
}

