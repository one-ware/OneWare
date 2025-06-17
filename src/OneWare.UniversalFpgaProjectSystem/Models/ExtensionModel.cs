using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;

namespace OneWare.UniversalFpgaProjectSystem.Models
{
    public class ExtensionModel : ObservableObject, IHardwareModel
    {
        private readonly ILogger _logger;
        private HardwareInterfaceModel? _parentInterfaceModel;
        private bool _isSelected;

        public ExtensionModel(IFpgaExtension fpgaExtension, ILogger logger)
        {
            FpgaExtension = fpgaExtension;
            _logger = logger;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public Dictionary<string, HardwarePinModel> PinModels { get; } = new();

        public Dictionary<string, HardwareInterfaceModel> InterfaceModels { get; } = new();

        public IFpgaExtension FpgaExtension { get; }

        public HardwareInterfaceModel? ParentInterfaceModel
        {
            get => _parentInterfaceModel;
            set
            {
                if (SetProperty(ref _parentInterfaceModel, value) && _parentInterfaceModel != null)
                {
                    try
                    {
                        foreach (var pin in FpgaExtension.Pins)
                        {
                            PinModels[pin.Name] = _parentInterfaceModel.TranslatedPins[pin.InterfacePin!];
                        }
                        foreach (var fpgaInterface in FpgaExtension.Interfaces)
                        {
                            InterfaceModels.TryAdd(fpgaInterface.Name, new HardwareInterfaceModel(fpgaInterface, this));
                            InterfaceModels[fpgaInterface.Name].TranslatePins();
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e.Message, e);
                    }
                }
            }
        }
    }
}
