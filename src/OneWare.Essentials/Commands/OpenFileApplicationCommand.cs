using System.ComponentModel;
using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Commands;

public class OpenFileApplicationCommand : ApplicationCommandBase
{
    private readonly string _file;

    public OpenFileApplicationCommand(string file) : base(file)
    {
        _file = file;
    }

    public override bool Execute(ILogical source)
    {
        var file = ContainerLocator.Container.Resolve<IProjectExplorerService>().GetEntry(_file) as IProjectFile;
        if (file == null) return false;
        _ = ContainerLocator.Container.Resolve<IMainDockService>().OpenFileAsync(file);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return true;
    }
}