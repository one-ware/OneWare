using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.ApplicationCommands.Models;

public class OpenFileApplicationCommand : IApplicationCommand
{
    public string Name { get; }
    public KeyGesture? Gesture { get; init; }
    public IImage? Image { get; }

    private IProjectFile _file;

    public OpenFileApplicationCommand(IProjectFile file)
    {
        _file = file;
        Name = Path.Combine(file.Root.Header, file.RelativePath);
        Image = file.Icon;
    }
    
    public bool Execute(ILogical source)
    {
        _ = ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(_file);
        return true;
    }

    public bool CanExecute(ILogical source)
    {
        return true;
    }
}