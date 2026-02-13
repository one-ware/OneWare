using System.Text.RegularExpressions;

namespace OneWare.Essentials.Models;

public interface ISettingValidation
{
    bool Validate(object? value, out string? warningMessage);
}

public static class StaticSettingValidations
{
    public static ISettingValidation IsNotNullOrWhiteSpace { get; } 
        = new IsNotNullOrWhiteSpaceValidation();
    
    private class IsNotNullOrWhiteSpaceValidation : ISettingValidation
    {
        private const string Message = "\u26A0 Field cannot be blank.";
        
        public bool Validate(object? value, out string? warningMessage)
        {
            string? str = value?.ToString();
            warningMessage = string.IsNullOrWhiteSpace(str) ? Message : null;
            return warningMessage is null;
        }
    }
}
    
public class RegexValidation(string regex, string message) : ISettingValidation
{
    private readonly Regex _regex = new(regex);
    
    public bool Validate(object? value, out string? warningMessage)
    {
        string? str = value?.ToString();
        if (str is null)
        {
            warningMessage = null;
            return true;
        }
        
        warningMessage = _regex.IsMatch(str) ? null : message;
        return warningMessage is null;
    }
}