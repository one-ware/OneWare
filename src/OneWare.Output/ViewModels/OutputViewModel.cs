namespace OneWare.Output.ViewModels;

public class OutputViewModel : OutputBaseViewModel, IOutputService
{
    public const string IconKey = "Material.Console";
    public OutputViewModel() : base(IconKey)
    {
        Id = "Output";
        Title = "Output";
    }
}