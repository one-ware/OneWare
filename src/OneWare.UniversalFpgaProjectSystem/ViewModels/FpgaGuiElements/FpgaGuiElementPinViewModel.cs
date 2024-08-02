using OneWare.UniversalFpgaProjectSystem.Fpga.Gui;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.ViewModels.FpgaGuiElements;

public class FpgaGuiElementPinViewModel : FpgaGuiElementViewModelBase
{
    public FpgaPinModel? PinModel { get; set; }
    
    public FpgaGuiElementPinViewModel(FpgaModel model, FpgaGuiElement element) : base(model, element)
    {
        if (model.PinModels.TryGetValue(element.Bind ?? "", out var pinModel))
        {
            PinModel = pinModel;
        }
    }
}