using Avalonia.Input;
using Avalonia.LogicalTree;
using OneWare.SDK.Extensions;

namespace OneWare.ApplicationCommands.Models;

/// <summary>
/// Command for Logical
/// </summary>
/// <param name="name">Name</param>
/// <param name="gesture">KeyGesture</param>
/// <param name="action">Action to Execute with Logical as parameter</param>
/// <typeparam name="T">Logical on which the Gesture is valid</typeparam>
public class LogicalApplicationCommand<T>(string name, KeyGesture gesture, Action<T> action) : IApplicationCommand where T : class
{
    public string Name { get; } = name;
    
    public KeyGesture Gesture { get; set; } = gesture;

    public Action<T> Action { get; } = action;
    
    public void Execute(ILogical source)
    {
        if (source.FindLogicalAncestorOfType<T>() is { } src)
        {
            Action?.Invoke(src);
        }
    }
}