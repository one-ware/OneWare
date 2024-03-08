namespace OneWare.Output.Views;

public partial class OutputView : OutputBaseView
{
    public OutputView()
    {
        InitializeComponent();

        Output.Options.AllowScrollBelowDocument = false;
    }
}