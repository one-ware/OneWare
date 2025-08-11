using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Models;

public class VirtualDialogModel
{
    public VirtualDialogModel(FlexibleWindow dialog)
    {
        Dialog = dialog;
    }

    public FlexibleWindow Dialog { get; }

    public void Close()
    {
        if (Dialog is { DataContext: FlexibleWindowViewModelBase vm })
            if(vm.OnWindowClosing(Dialog)) vm.Close(Dialog);
        else Dialog.Close();
    }
}