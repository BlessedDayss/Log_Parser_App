using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters.Base;

public abstract class BaseConverter : IValueConverter
{
    public abstract object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture);

    public virtual object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException($"{GetType().Name} does not support ConvertBack operation.");
    }

    protected static T ConvertOrDefault<T>(object? value, T defaultValue = default!)
    {
        if (value is T typedValue)
            return typedValue;

        try
        {
            return (T)System.Convert.ChangeType(value, typeof(T))!;
        }
        catch
        {
            return defaultValue;
        }
    }
}
