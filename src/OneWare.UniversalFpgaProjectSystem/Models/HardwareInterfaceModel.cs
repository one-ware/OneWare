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
    
    public HardwareInterfaceModel(HardwareInterface fpgaInterface, IHardwareModel owner)
    {
        Interface = fpgaInterface;
        Owner = owner;

        TranslatePins();
        UpdateMenu();
    }
    
    public IHardwareModel Owner { get; }

    public FpgaModel? FpgaModel
    {
        get
        {
            var parent = Owner;
            while (parent != null)
            {
                if (parent is FpgaModel fpgaModel) return fpgaModel;
                parent = (parent as ExtensionModel)?.ParentInterfaceModel?.Owner;
            }
            return null;
        }
    }
    
    public Dictionary<string, HardwarePinModel> TranslatedPins { get; } = new();

    public HardwareInterface Interface { get; }

    public ObservableCollection<MenuItemViewModel> InterfaceMenu { get; } = new();

    public ExtensionModel? ConnectedExtension
    {
        get => _connectedExtension;
        private set => SetProperty(ref _connectedExtension, value);
    }

    public ExtensionViewModelBase? ConnectedExtensionViewModel
    {
        get => _connectedExtensionViewModel;
        private set
        {
            _connectedExtensionViewModel?.Dispose();
            SetProperty(ref _connectedExtensionViewModel, value);
        }
    }

    public void TranslatePins()
    {
        foreach (var pin in Interface.Pins)
        {
            TranslatedPins[pin.Name] = Owner.PinModels[pin.BindPin!];
        }
                
        if (ConnectedExtension != null)
        {
            ConnectedExtension.ParentInterfaceModel = this;
        }
    }
    
    private void UpdateMenu()
    {
        var fpgaService = ContainerLocator.Container.Resolve<FpgaService>();

        InterfaceMenu.Clear();

        if (ConnectedExtension != null)
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
            ConnectedExtension = new ExtensionModel(extensionPackage.LoadExtension())
            {
                ParentInterfaceModel = this
            };
            ConnectedExtensionViewModel = extensionPackage.LoadExtensionViewModel(ConnectedExtension);
        }
        else
        {
            ConnectedExtension = null;
            ConnectedExtensionViewModel = null;
        }

        Dispatcher.UIThread.Post(UpdateMenu);
    }

    public void DropExtension(HardwareInterfaceModel lastOwner)
    {
        var connections = lastOwner.TranslatedPins!.Where(x => x.Value.ConnectedNode != null).ToList();
        
        ConnectedExtension = lastOwner.ConnectedExtension;
        ConnectedExtension!.ParentInterfaceModel = this;
        ConnectedExtensionViewModel = lastOwner.ConnectedExtensionViewModel;
        ConnectedExtensionViewModel?.Initialize();

        // Autoconnect pins
        if (FpgaModel is { } model)
        {
            foreach (var connection in connections)
            {
                var pin = TranslatedPins[connection.Key];
                var node = connection.Value.ConnectedNode;
                model.Disconnect(connection.Value);
                model.Connect(pin, node!);
            }
        }
        
        lastOwner.SetExtension(null);

        Dispatcher.UIThread.Post(UpdateMenu);
    }
}