using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaInterfaceModel : ObservableObject
{
    private FpgaExtensionModel? _connection;

    public FpgaInterfaceModel(FpgaInterface fpgaInterface, FpgaModel parent)
    {
        Interface = fpgaInterface;
        Parent = parent;

        UpdateMenu();
    }

    public FpgaInterface Interface { get; }

    public FpgaModel Parent { get; }

    public ObservableCollection<MenuItemViewModel> InterfaceMenu { get; } = new();

    public FpgaExtensionModel? Connection
    {
        get => _connection;
        set => SetProperty(ref _connection, value);
    }

    private void UpdateMenu()
    {
        var fpgaService = ContainerLocator.Container.Resolve<FpgaService>();

        InterfaceMenu.Clear();

        if (Connection != null)
        {
            InterfaceMenu.Add(new MenuItemViewModel("Disconnect")
            {
                Header = "Disconnect",
                Command = new RelayCommand<IFpgaExtension>(SetExtension),
                CommandParameter = null
            });
            return;
        }

        foreach (var ext in fpgaService.FpgaExtensions)
        {
            if (ext.Connector != Interface.Connector) continue;

            InterfaceMenu.Add(new MenuItemViewModel($"Connect {ext.Name}")
            {
                Header = $"Connect {ext.Name}",
                Command = new RelayCommand<IFpgaExtension>(SetExtension),
                CommandParameter = ext
            });
        }
    }

    private void SetExtension(IFpgaExtension? extension)
    {
        if (extension == null)
        {
            if (Connection != null)
            {
                Connection.Parent = null;
                Connection = null;
            }
        }
        else
        {
            var fpgaService = ContainerLocator.Container.Resolve<FpgaService>();

            fpgaService.FpgaExtensionViewModels.TryGetValue(extension, out var custom);

            if (custom != null)
            {
                var model = Activator.CreateInstance(custom, extension) as FpgaExtensionModel;

                Connection = model ?? throw new NullReferenceException(nameof(model));
                Connection.Parent = this;
            }
        }

        Dispatcher.UIThread.Post(UpdateMenu);
    }
}