using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class ExtensionModel : ObservableObject, IHardwareModel
{
    private HardwareInterfaceModel? _parentInterfaceModel;

    private bool _isSelected;
    
    public ExtensionModel(IFpgaExtension fpgaExtension)
    {
        FpgaExtension = fpgaExtension;
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
            SetProperty(ref _parentInterfaceModel, value);

            try
            {
                if (_parentInterfaceModel != null)
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
            }
            catch (Exception e)
            {
                AppServices.Logger.LogError(e, e.Message);
            }
        }
    }
}