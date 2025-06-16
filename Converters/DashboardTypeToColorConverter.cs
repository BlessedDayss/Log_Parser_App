using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Log_Parser_App.Services.Dashboard;

namespace Log_Parser_App.Converters
{
    public class DashboardTypeToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DashboardType currentType && parameter is string targetTypeString)
            {
                if (Enum.TryParse<DashboardType>(targetTypeString, out var targetDashboardType))
                {
                    return currentType == targetDashboardType 
                        ? new SolidColorBrush(Color.Parse("#3B82F6")) // Active blue
                        : new SolidColorBrush(Color.Parse("#374151")); // Inactive gray
                }
            }
            
            return new SolidColorBrush(Color.Parse("#374151")); // Default gray
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 