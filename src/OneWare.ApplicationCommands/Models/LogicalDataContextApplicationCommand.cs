using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.SDK.Extensions;

namespace OneWare.ApplicationCommands.Models;

/// <summary>
/// Command for Gesture DataContext
/// </summary>
/// <param name="name">Name</param>
/// <param name="gesture">KeyGesture</param>
/// <param name="action">Action to Execute with DataContext as parameter</param>
/// <typeparam name="T">Logical DataContext on which the Gesture is valid</typeparam>
public class LogicalDataContextApplicationCommand<T>(string name, KeyGesture gesture, Action<T> action) : IApplicationCommand where T : class
{
    public string Name { get; } = name;
    
    public KeyGesture Gesture { get; set; } = gesture;

    public Action<T> Action { get; } = action;
    
    public void Execute(ILogical source)
    {
        if (source.FindLogicalAncestorWithDataContextType<T>() is { } src)
        {
            Action?.Invoke(src);
        }
    }
}