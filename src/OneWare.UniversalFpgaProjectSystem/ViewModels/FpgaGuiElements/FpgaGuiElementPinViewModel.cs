using Avalonia.Media;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementRectViewModel
{
    public static IBrush Brush3V = Brushes.LightCoral;
    
    public static IBrush Brush5V = Brushes.Red;
    
    public static IBrush BrushGnd = Brushes.Cyan;
    
    private const int DefaultWidth = 10;

    private const int DefaultHeight = 10;
    
    private IHardwareModel? _parent;

    public IHardwareModel? Parent
    {
        get => _parent;
        set
        {
            SetProperty(ref _parent, value);

            if (Bind != null && _parent != null)
            {
                if(_parent.PinModels.TryGetValue(Bind, out var model))
                    PinModel = model;
            }
        }
    }

    public string? Bind { get; init; }
    
    private HardwarePinModel? _pinModel;

    public HardwarePinModel? PinModel
    {
        get => _pinModel;
        private set => SetProperty(ref _pinModel, value);
    }

    public FpgaGuiElementPinViewModel(int x, int y, int width, int height) : base(x, y,
        width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height)
    {
    }
}