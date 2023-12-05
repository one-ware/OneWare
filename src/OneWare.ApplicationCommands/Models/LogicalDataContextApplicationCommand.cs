using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.SDK.Extensions;
using OneWare.SDK.Models;

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
    
    public IImage? Image { get; init; }

    private Action<T> Action { get; } = action;
    
    public bool Execute(ILogical source)
    {
        if (source.FindLogicalAncestorWithDataContextType<T>() is { } src)
        {
            Action?.Invoke(src);
            return true;
        }
        return false;
    }
}