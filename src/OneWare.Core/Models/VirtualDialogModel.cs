﻿using OneWare.Essentials.Controls;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Models;

public class VirtualDialogModel
{
    public FlexibleWindow Dialog { get; }

    public VirtualDialogModel(FlexibleWindow dialog)
    {
        Dialog = dialog;
    }

    public void Close()
    {
        if(Dialog is {DataContext: FlexibleWindowViewModelBase vm})
            vm.Close(Dialog);
        else Dialog.Close();
    }
}