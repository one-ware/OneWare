using Avalonia.Media;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementRectViewModel
{
    private const int DefaultWidth = 10;

    private const int DefaultHeight = 10;

    public string? Bind { get; init; }

    public bool FlipLabel { get; init; }
    
    private HardwarePinModel? _pinModel;

    public HardwarePinModel? PinModel
    {
        get => _pinModel;
        private set => SetProperty(ref _pinModel, value);
    }

    public FpgaGuiElementPinViewModel(double x, double y, double width, double height) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height)
    {
    }

    public override void Initialize()
    {
        base.Initialize();
        
        if (Bind != null && Parent != null)
        {
            if(Parent.PinModels.TryGetValue(Bind, out var model))
                PinModel = model;
            else ContainerLocator.Container.Resolve<ILogger>().Error("Pin not found: " + Bind);
        }
    }
}