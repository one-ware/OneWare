namespace OneWare.Output.ViewModels;

public class OutputViewModel : OutputBaseViewModel, IOutputService
{
    public OutputViewModel()
    {
        Id = "Output";
        Title = "Output";
    }
}