using System.ComponentModel;
using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;


namespace OneWare.Essentials.Commands;

public class OpenFileApplicationCommand : ApplicationCommandBase
{
    private readonly IProjectFile _file;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDockService _dockService;

    public OpenFileApplicationCommand(IProjectFile file,
        IDockService dockService) 
           : base(Path.Combine(file.Root.Header, file.RelativePath))
    {
        _dockService = dockService;
        _file = file;

        if (file is INotifyPropertyChanged obs) IconObservable = obs.WhenValueChanged(x => (x as IProjectFile)!.Icon);
    }

    public override bool Execute(ILogical source)
    {
        _ = _dockService.OpenFileAsync(_file);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return true;
    }
}