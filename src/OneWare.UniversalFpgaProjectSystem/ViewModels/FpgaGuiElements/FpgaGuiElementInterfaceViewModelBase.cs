using System.Runtime.InteropServices;
using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public abstract class FpgaGuiElementInterfaceViewModelBase : FpgaGuiElementViewModelBase
{
    private IHardwareModel? _parent;

    public IHardwareModel? Parent
    {
        get => _parent;
        set
        {
            SetProperty(ref _parent, value);

            if (Bind != null && _parent != null)
            {
                if(_parent.InterfaceModels.TryGetValue(Bind, out var model))
                    InterfaceModel = model;
            }
        }
    }
    
    public string? Bind { get; init; }
    
    private HardwareInterfaceModel? _interfaceModel;
    public HardwareInterfaceModel? InterfaceModel
    {
        get => _interfaceModel;
        private set
        {
            SetProperty(ref _interfaceModel, value);
            
            if(_interfaceModel == null)
                return;

            foreach (var (key, model) in _interfaceModel.PinModels)
            {
                PinViewModels.TryAdd(key, new FpgaGuiElementPinViewModel(0, 0, 0, 0)
                {
                    Color = Brushes.Yellow,
                    Bind = model.Pin.Name
                });
                PinViewModels[key].Parent = Parent;
            }
        }
    }

    public Dictionary<string, FpgaGuiElementPinViewModel> PinViewModels { get; } = new();

    public FpgaGuiElementInterfaceViewModelBase(int x, int y) : base(x, y)
    {
        PinViewModels["GND"] = new FpgaGuiElementPinViewModel(0, 0, 0, 0)
        {
            Color = FpgaGuiElementPinViewModel.BrushGnd
        };
        
        PinViewModels["3V"] = new FpgaGuiElementPinViewModel(0, 0, 0, 0)
        {
            Color = FpgaGuiElementPinViewModel.Brush3V
        };
    }
}