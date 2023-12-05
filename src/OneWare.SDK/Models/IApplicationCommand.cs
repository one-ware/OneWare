using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace OneWare.SDK.Models;

public interface IApplicationCommand
{
    public string Name { get; }
    
    public KeyGesture? Gesture { get; }
    
    public IImage? Image { get; }
    
    public bool Execute(ILogical source);
}