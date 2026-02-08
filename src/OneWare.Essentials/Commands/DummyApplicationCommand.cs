using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.Commands;

public class DummyApplicationCommand : IApplicationCommand
{
    public string Name { get; } = "";
    public string? Detail { get; } = null;

    public KeyGesture? ActiveGesture { get; set; } = null;
    public KeyGesture? DefaultGesture { get; } = null;

    public IconModel? Icon { get; } = null;

    public bool Execute(ILogical source)
    {
        return false;
    }

    public bool CanExecute(ILogical source)
    {
        return false;
    }
}