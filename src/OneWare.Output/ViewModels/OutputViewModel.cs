using OneWare.SDK.Services;

namespace OneWare.Output.ViewModels;

public class OutputViewModel : OutputBaseViewModel, IOutputService
{
    public const string IconKey = "VSCodeLight.run";
    public OutputViewModel() : base(IconKey)
    {
        Id = "Output";
        Title = "Output";
    }
}