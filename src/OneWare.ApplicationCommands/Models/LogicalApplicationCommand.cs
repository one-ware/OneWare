using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.SDK.Extensions;
using OneWare.SDK.Models;

namespace OneWare.ApplicationCommands.Models;

/// <summary>
/// Command for Logical
/// </summary>
/// <param name="name">Name</param>
/// <param name="action">Action to Execute with Logical as parameter</param>
/// <param name="gesture">KeyGesture</param>
/// <typeparam name="T">Logical on which the Gesture is valid</typeparam>
public class LogicalApplicationCommand<T>(string name, Action<T> action, KeyGesture? gesture) : IApplicationCommand where T : class
{
    public string Name { get; } = name;
    
    public KeyGesture? Gesture { get; set; } = gesture;
    
    public IImage? Image { get; init; }

    private Action<T> Action { get; } = action;
    
    public bool Execute(ILogical source)
    {
        if (source is T src)
        {
            Action.Invoke(src);
            return true;
        }
        else if (source.FindLogicalAncestorOfType<T>() is { } ancestor)
        {
            Action.Invoke(ancestor);
            return true;
        }
        return false;
    }

    public bool CanExecute(ILogical source)
    {
        return source is T || source.FindLogicalAncestorOfType<T>() is not null;
    }
}