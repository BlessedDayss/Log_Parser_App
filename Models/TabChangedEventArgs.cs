using System;
using Log_Parser_App.ViewModels;

namespace Log_Parser_App.Models
{
    /// <summary>
    /// Event arguments for tab changed events
    /// </summary>
    public class TabChangedEventArgs : EventArgs
    {
        public TabViewModel? OldTab { get; }
        public TabViewModel? NewTab { get; }

        public TabChangedEventArgs(TabViewModel? oldTab, TabViewModel? newTab)
        {
            OldTab = oldTab;
            NewTab = newTab;
        }
    }
} 