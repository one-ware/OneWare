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

public class HardwareInterfaceModel : ObservableObject
{
    private ExtensionModel? _connectedExtension;

    private ExtensionViewModelBase? _connectedExtensionViewModel;
    
    public HardwareInterfaceModel(HardwareInterface fpgaInterface, IHardwareModel parent, FpgaModel fpgaModel)
    {
        Interface = fpgaInterface;
        FpgaModel = fpgaModel;

        foreach (var pin in fpgaInterface.Pins)
        {
            PinModels.Add(pin.Name, parent.PinModels[pin.HardwarePin.Name]);
        }

        UpdateMenu();
    }
    
    public FpgaModel FpgaModel { get; }

    public Dictionary<string, HardwarePinModel> PinModels { get; } = new();
    
    public HardwareInterface Interface { get; }

    public ObservableCollection<MenuItemViewModel> InterfaceMenu { get; } = new();

    public ExtensionModel? Connection
    {
        get => _connectedExtension;
        private set => SetProperty(ref _connectedExtension, value);
    }

    public ExtensionViewModelBase? ConnectionViewModel
    {
        get => _connectedExtensionViewModel;
        private set
        {
            _connectedExtensionViewModel?.Dispose();
            SetProperty(ref _connectedExtensionViewModel, value);
        }
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

    public void SetExtension(IFpgaExtensionPackage? extensionPackage)
    {
        if (extensionPackage != null)
        {
            Connection = new ExtensionModel(extensionPackage.LoadExtension(), this, FpgaModel);
            ConnectionViewModel = extensionPackage.LoadExtensionViewModel(Connection);
        }
        else
        {
            Connection = null;
            ConnectionViewModel = null;
        }

        Dispatcher.UIThread.Post(UpdateMenu);
    }
    
    public void DropExtension(HardwareInterfaceModel lastOwner)
    {
        Connection = lastOwner.Connection;
        ConnectionViewModel = lastOwner.ConnectionViewModel;
        Connection!.Parent = this;
        
        lastOwner.SetExtension(null);
        
        Dispatcher.UIThread.Post(UpdateMenu);
    }
}