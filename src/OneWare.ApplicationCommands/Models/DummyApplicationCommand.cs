using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.SDK.Extensions;
using OneWare.SDK.Models;

namespace OneWare.ApplicationCommands.Models;

public class DummyApplicationCommand : IApplicationCommand
{
    public string Name { get; } = "";
    
    public KeyGesture? Gesture { get; } = null;

    public IImage? Image { get; } = null;
    
    public bool Execute(ILogical source)
    {
        return false;
    }

    public bool CanExecute(ILogical source)
    {
        return false;
    }
}