namespace Log_Parser_App.Services
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;


    public interface ILocalizationService : INotifyPropertyChanged
    {
        CultureInfo CurrentCulture { get; }

        IReadOnlyList<CultureInfo> AvailableCultures { get; }

        void ChangeCulture(CultureInfo culture);
        string GetString(string key);
    }
}