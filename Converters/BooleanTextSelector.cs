using Log_Parser_App.Converters.Interfaces;

namespace Log_Parser_App.Converters;

public class BooleanTextSelector : IBooleanTextSelector
{
    public string SelectText(bool condition, string trueText, string falseText)
    {
        return condition ? trueText : falseText;
    }
} 