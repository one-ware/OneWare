using DynamicData.Binding;
using OneWare.Essentials.Controls;

namespace OneWare.UniversalFpgaProjectSystem.Views;

public partial class UniversalFpgaProjectCompileView : FlexibleWindow
{
    public UniversalFpgaProjectCompileView()
    {
        InitializeComponent();

        VisiblePinDataGrid.WhenValueChanged(x => x.SelectedItem).Subscribe(x =>
        {
            if(x is not null) VisiblePinDataGrid.ScrollIntoView(x, null);
        });
    }
}