using Avalonia.Media;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements
{
    public enum PinLabelPosition
    {
        Before,
        Inside,
        After
    }

    public class FpgaGuiElementPinViewModel : FpgaGuiElementRectViewModel
    {
        private const int DefaultWidth = 10;
        private const int DefaultHeight = 10;
        private readonly ILogger _logger;

        public string? Bind { get; init; }

        public PinLabelPosition LabelPosition { get; init; }

        private HardwarePinModel? _pinModel;

        public HardwarePinModel? PinModel
        {
            get => _pinModel;
            private set => SetProperty(ref _pinModel, value);
        }

        public FpgaGuiElementPinViewModel(double x, double y, double width, double height, ILogger logger) : base(x, y,
            width == 0 ? DefaultWidth : width, height == 0 ? DefaultHeight : height)
        {
            _logger = logger;
        }

        public override void Initialize()
        {
            base.Initialize();

            if (Bind != null && Parent != null)
            {
                if (!Parent.PinModels.TryGetValue(Bind, out var model))
                {
                    _logger.Error("Pin not found: " + Bind);
                }
                else
                {
                    PinModel = model;
                }
            }
        }
    }
}
