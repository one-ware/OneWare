using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using Prism.Ioc;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class ExtensionModel : ObservableObject, IHardwareModel
{
    private HardwareInterfaceModel _parent;

    private bool _isSelected;
    
    public ExtensionModel(IFpgaExtension fpgaExtension, HardwareInterfaceModel parent, FpgaModel fpgaModel)
    {
        FpgaExtension = fpgaExtension;
        _parent = parent;

        try
        {
            foreach (var pin in fpgaExtension.Pins) 
                PinModels.Add(pin.Name, parent.PinModels[pin.InterfacePin!]);
        
            foreach (var fpgaInterface in fpgaExtension.Interfaces) 
                InterfaceModels.Add(fpgaInterface.Name, new HardwareInterfaceModel(fpgaInterface, this, fpgaModel));
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
        }
    }
    
    public FpgaModel FpgaModel => _parent.FpgaModel;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public Dictionary<string, HardwarePinModel> PinModels { get; } = new();
    
    public Dictionary<string, HardwareInterfaceModel> InterfaceModels { get; } = new();
    
    public IFpgaExtension FpgaExtension { get; }

    public HardwareInterfaceModel Parent
    {
        get => _parent;
        set => SetProperty(ref _parent, value);
    }
}