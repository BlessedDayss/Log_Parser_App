using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace LogParserApp.Services
{
    /// <summary>
    /// Interface for the localization service
    /// </summary>
    public interface ILocalizationService : INotifyPropertyChanged
    {
        /// <summary>
        /// Current language culture
        /// </summary>
        CultureInfo CurrentCulture { get; }
        
        /// <summary>
        /// Available cultures for the application
        /// </summary>
        IReadOnlyList<CultureInfo> AvailableCultures { get; }
        
        /// <summary>
        /// Change the current language
        /// </summary>
        void ChangeCulture(CultureInfo culture);
        
        /// <summary>
        /// Get a localized string by key
        /// </summary>
        string GetString(string key);
    }
}