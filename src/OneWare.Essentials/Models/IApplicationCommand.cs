using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace OneWare.Essentials.Models;

public interface IApplicationCommand
{
    public string Name { get; }
    
    public KeyGesture? ActiveGesture { get; set; }
    
    public KeyGesture? DefaultGesture { get; }
    
    public IImage? Icon { get; }
    
    public bool Execute(ILogical source);
    
    public bool CanExecute(ILogical source);
}