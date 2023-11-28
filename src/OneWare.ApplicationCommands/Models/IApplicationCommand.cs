using Avalonia.Input;
using Avalonia.LogicalTree;

namespace OneWare.ApplicationCommands.Models;

public interface IApplicationCommand
{
    public string Name { get; }
    
    public KeyGesture? Gesture { get; set; }
    
    public void Execute(ILogical source);
}