using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Helpers;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public abstract class FpgaGuiElementInterfaceViewModelBase : FpgaGuiElementViewModelBase
{
    private HardwareInterfaceModel? _interfaceModel;

    public FpgaGuiElementInterfaceViewModelBase(double x, double y) : base(x, y)
    {
        PinViewModels["GND"] = new FpgaGuiElementPinViewModel(0, 0, 0, 0)
        {
            Color = HardwareGuiCreator.ColorShortcuts["GND"]
        };

        PinViewModels["3V3"] = new FpgaGuiElementPinViewModel(0, 0, 0, 0)
        {
            Color = HardwareGuiCreator.ColorShortcuts["3V3"]
        };
    }

    public string? ConnectorStyle { get; init; }
    public string? Bind { get; init; }

    public HardwareInterfaceModel? InterfaceModel
    {
        get => _interfaceModel;
        private set
        {
            SetProperty(ref _interfaceModel, value);

            if (_interfaceModel == null) return;

            foreach (var pin in _interfaceModel.Interface.Pins)
            {
                PinViewModels.TryAdd(pin.Name, new FpgaGuiElementPinViewModel(0, 0, 0, 0)
                {
                    Color = Brushes.Yellow,
                    Bind = pin.BindPin,
                    Parent = Parent
                });
                PinViewModels[pin.Name].Initialize();

                _interfaceModel.ConnectedExtensionViewModel?.Initialize();
            }
        }
    }

    public Dictionary<string, FpgaGuiElementPinViewModel> PinViewModels { get; } = new();

    public override void Initialize()
    {
        base.Initialize();

        if (Bind != null && Parent != null)
            if (Parent.InterfaceModels.TryGetValue(Bind, out var model))
                InterfaceModel = model;
    }
}