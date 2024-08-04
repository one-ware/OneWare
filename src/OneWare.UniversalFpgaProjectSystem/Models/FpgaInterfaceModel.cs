using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaInterfaceModel : ObservableObject
{
    private IFpgaExtensionPackage? _connectedPackage;
    
    private FpgaExtensionModel? _connection;

    private FpgaExtensionViewModelBase? _connectionViewModel;

    public FpgaInterfaceModel(FpgaInterface fpgaInterface, FpgaModel parent)
    {
        Interface = fpgaInterface;
        Parent = parent;

        UpdateMenu();
    }

    public FpgaInterface Interface { get; }

    public FpgaModel Parent { get; }

    public ObservableCollection<MenuItemViewModel> InterfaceMenu { get; } = new();
    
    public IFpgaExtensionPackage? ConnectedPackage
    {
        get => _connectedPackage;
        set
        {
            SetProperty(ref _connectedPackage, value);

            if (_connectedPackage != null)
            {
                Connection = new FpgaExtensionModel(_connectedPackage.LoadExtension());
                ConnectionViewModel = _connectedPackage.LoadExtensionViewModel(Connection);
            }
            else
            {
                Connection = null;
                ConnectionViewModel = null;
            }
        }
    }

    public FpgaExtensionModel? Connection
    {
        get => _connection;
        private set
        {
            if (_connection != null) _connection.Parent = null;
            SetProperty(ref _connection, value);
        }
    }

    public FpgaExtensionViewModelBase? ConnectionViewModel
    {
        get => _connectionViewModel;
        private set => SetProperty(ref _connectionViewModel, value);
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
                Command = new RelayCommand<IFpgaExtensionPackage?>(SetExtension),
                CommandParameter = null
            });
            return;
        }

        foreach (var ext in fpgaService.FpgaExtensionPackages)
        {
            if (ext.Connector != Interface.Connector) continue;

            InterfaceMenu.Add(new MenuItemViewModel($"Connect {ext.Name}")
            {
                Header = $"Connect {ext.Name}",
                Command = new RelayCommand<IFpgaExtensionPackage>(SetExtension),
                CommandParameter = ext
            });
        }
    }

    private void SetExtension(IFpgaExtensionPackage? extensionPackage)
    {
        ConnectedPackage = extensionPackage;

        Dispatcher.UIThread.Post(UpdateMenu);
    }
}