using System.ComponentModel;
using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.SDK.Models;
using OneWare.SDK.Services;
using Prism.Ioc;

namespace OneWare.SDK.Commands;

public class OpenFileApplicationCommand : ApplicationCommandBase
{
    private readonly IProjectFile _file;
    
    public OpenFileApplicationCommand(IProjectFile file) : base(Path.Combine(file.Root.Header, file.RelativePath))
    {
        _file = file;

        if (file is INotifyPropertyChanged obs)
        {
            IconObservable = obs.WhenValueChanged(x => (x as IProjectFile)!.Icon);
        }
    }
    
    public override bool Execute(ILogical source)
    {
        _ = ContainerLocator.Container.Resolve<IDockService>().OpenFileAsync(_file);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return true;
    }
}