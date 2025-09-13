using System;
using System.Globalization;
using Log_Parser_App.Converters.Base;
using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters;

public class StringShortenerConverter : BaseTypedConverter<string?, string?>, IStringConverter
{
    private readonly ITextShortener _textShortener;
    private readonly IBooleanTextSelector _booleanTextSelector;

    public StringShortenerConverter()
    {
        _textShortener = new TextShortener();
        _booleanTextSelector = new BooleanTextSelector();
    }

    public StringShortenerConverter(ITextShortener textShortener, IBooleanTextSelector booleanTextSelector)
    {
        ArgumentNullException.ThrowIfNull(textShortener);
        ArgumentNullException.ThrowIfNull(booleanTextSelector);

        _textShortener = textShortener;
        _booleanTextSelector = booleanTextSelector;
    }

    public override string? Convert(string? value, CultureInfo? culture = null)
    {
        return value;
    }

    public override object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return null;

        string parameterString = parameter.ToString() ?? string.Empty;

        if (parameterString.Contains('|'))
        {
            string[] options = parameterString.Split('|');
            if (options.Length == 2)
            {
                bool condition = value is bool boolVal && boolVal;
                return _booleanTextSelector.SelectText(condition, options[0], options[1]);
            }
        }

        if (value is string strValue && !string.IsNullOrEmpty(strValue))
        {
            if (int.TryParse(parameterString, out int maxLength) && strValue.Length > maxLength)
            {
                return _textShortener.ShortenText(strValue, maxLength);
            }
            return strValue;
        }

        return null;
    }

    protected override string? GetDefaultOutput() => null;
} 