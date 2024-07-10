using Avalonia.LogicalTree;

namespace OneWare.Essentials.Commands;

/// <summary>
/// Command for Logical
/// </summary>
/// <param name="name">Name</param>
/// <param name="action">Action to Execute with Logical as parameter</param>
/// <typeparam name="T">Logical on which the Gesture is valid</typeparam>
public class LogicalApplicationCommand<T>(string name, Action<T> action) : 
    ApplicationCommandBase(name) where T : class, ILogical
{
    private Action<T> Action { get; } = action;
    
    public override bool Execute(ILogical source)
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

    public override bool CanExecute(ILogical source)
    {
        return source is T || source.FindLogicalAncestorOfType<T>() is not null;
    }
}