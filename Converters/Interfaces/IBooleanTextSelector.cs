namespace Log_Parser_App.Converters.Interfaces;

public interface IBooleanTextSelector
{
    string SelectText(bool condition, string trueText, string falseText);
}
