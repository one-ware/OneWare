using Avalonia;
using Avalonia.LogicalTree;
using OneWare.Essentials.Extensions;

namespace OneWare.Essentials.Commands;

/// <summary>
/// Command for Gesture DataContext
/// </summary>
/// <param name="name">Name</param>
/// <param name="action">Action to Execute with Logical as parameter</param>
/// <typeparam name="T">Logical DataContext on which the Gesture is valid</typeparam>
public class LogicalDataContextApplicationCommand<T>(string name, Action<T> action) : ApplicationCommandBase(name) where T : class, ILogical
{
    private Action<T> Action { get; } = action;
    
    public override bool Execute(ILogical source)
    {
        if (source is StyledElement {DataContext: T dataContext})
        {
            Action.Invoke(dataContext);
            return true;
        }
        if (source.FindLogicalAncestorWithDataContextType<T>() is { } src)
        {
            Action.Invoke(src);
            return true;
        }
        return false;
    }

    public override bool CanExecute(ILogical source)
    {
        return source is StyledElement {DataContext: T} || source.FindLogicalAncestorWithDataContextType<T>() is not null;
    }
}