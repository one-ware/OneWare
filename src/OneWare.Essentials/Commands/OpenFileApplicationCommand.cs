using System.ComponentModel;
using Avalonia.LogicalTree;
using DynamicData.Binding;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Essentials.Commands;

public class OpenFileApplicationCommand : ApplicationCommandBase
{
    private readonly string _file;

    public OpenFileApplicationCommand(string file) : base(Path.GetFileNameWithoutExtension(file))
    {
        _file = file;
        Icon = new IconModel()
        {
            IconObservable = ContainerLocator.Container.Resolve<IFileIconService>()
                .GetFileIcon(Path.GetExtension(file))
        };
    }

    public override bool Execute(ILogical source)
    {
        _ = ContainerLocator.Container.Resolve<IMainDockService>().OpenFileAsync(_file);
        return true;
    }

    public override bool CanExecute(ILogical source)
    {
        return true;
    }
}
