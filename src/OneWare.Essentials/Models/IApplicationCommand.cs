using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace OneWare.Essentials.Models;

public interface IApplicationCommand
{
    public string Name { get; }
    
    public string? Detail { get; }

    public KeyGesture? ActiveGesture { get; set; }

    public KeyGesture? DefaultGesture { get; }

    public IconModel? Icon { get; }

    public bool Execute(ILogical source);

    public bool CanExecute(ILogical source);
}