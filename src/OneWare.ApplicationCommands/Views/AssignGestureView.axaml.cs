using OneWare.Essentials.Controls;
using OneWare.Essentials.Services; // Make sure this using directive is present to resolve IDockService

namespace OneWare.ApplicationCommands.Views;

public partial class AssignGestureView : FlexibleWindow
{
    // The constructor of FlexibleWindow requires an IDockService.
    // You need to accept it here and pass it to the base constructor.
    public AssignGestureView(IDockService dockService) : base(dockService)
    {
        InitializeComponent();
    }
}