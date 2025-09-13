using System.Globalization;
using Log_Parser_App.Converters.Base;
using Log_Parser_App.Converters.Interfaces;
using Log_Parser_App.Models;
using Log_Parser_App.ViewModels;

namespace Log_Parser_App.Converters;

public class TabToVisibilityConverter : BaseTypedConverter<TabViewModel, bool>, IVisibilityConverter<TabViewModel>
{
    public override bool Convert(TabViewModel value, CultureInfo? culture = null)
    {
        return false;
    }

    public override object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TabViewModel tabViewModel || parameter is not string mode)
        {
            return false;
        }

        return mode switch
        {
            "Standard" => tabViewModel.LogType == LogFormatType.Standard,
            "IIS" => tabViewModel.LogType == LogFormatType.IIS,
            _ => false
        };
    }

    protected override bool GetDefaultOutput() => false;
}
