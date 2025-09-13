using System.Globalization;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters.Base;

public abstract class BaseTypedConverter<TInput, TOutput> : BaseConverter, IConverter<TInput, TOutput>
{
    public abstract TOutput Convert(TInput value, CultureInfo? culture = null);

    public override object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TInput typedValue)
        {
            var result = Convert(typedValue, culture);
            return result;
        }

        if (typeof(TInput).IsClass && value == null)
        {
            return Convert(default!, culture);
        }

        if (typeof(TInput).IsValueType && value != null)
        {
            try
            {
                var convertedValue = System.Convert.ChangeType(value, typeof(TInput));
                if (convertedValue is TInput typedConvertedValue)
                {
                    return Convert(typedConvertedValue, culture);
                }
            }
            catch
            {
            }
        }

        return GetDefaultOutput();
    }

    protected virtual TOutput GetDefaultOutput() => default!;
}
