using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class HardwareInterfaceModel : ObservableObject
{
    private readonly ILogger _logger;
    private readonly FpgaService _fpgaService;

    private ExtensionModel? _connectedExtension;
    private ExtensionViewModelBase? _connectedExtensionViewModel;

    public HardwareInterfaceModel(HardwareInterface fpgaInterface, IHardwareModel owner, ILogger logger, FpgaService fpgaService)
    {
        Interface = fpgaInterface;
        Owner = owner;
        _logger = logger;
        _fpgaService = fpgaService;

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
        try
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
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    private void UpdateMenu()
    {
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

        foreach (var ext in _fpgaService.FpgaExtensionPackages)
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
            var extension = new ExtensionModel(extensionPackage.LoadExtension(), _logger)
            {
                ParentInterfaceModel = this
            };

            ConnectedExtension = extension;
            ConnectedExtensionViewModel = extensionPackage.LoadExtensionViewModel(extension);
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
        var connections = lastOwner.TranslatedPins!
            .Where(x => x.Value.ConnectedNode != null)
            .ToList();

        ConnectedExtension = lastOwner.ConnectedExtension;
        ConnectedExtension!.ParentInterfaceModel = this;
        ConnectedExtensionViewModel = lastOwner.ConnectedExtensionViewModel;
        ConnectedExtensionViewModel?.Initialize();

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
