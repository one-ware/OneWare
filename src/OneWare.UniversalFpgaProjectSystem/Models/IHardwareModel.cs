namespace OneWare.UniversalFpgaProjectSystem.Models;

public interface IHardwareModel
{
    public Dictionary<string, HardwarePinModel> PinModels { get; }
    
    public Dictionary<string, HardwareInterfaceModel> InterfaceModels { get; }
}