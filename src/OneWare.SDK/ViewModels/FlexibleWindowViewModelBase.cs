
using Dock.Model.Mvvm.Controls;
using OneWare.SDK.Controls;

namespace OneWare.SDK.ViewModels;

public class FlexibleWindowViewModelBase : Document
{
    private bool _isDirty;

    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }
    
    public virtual void Close(FlexibleWindow window)
    {
        IsDirty = false;
        window.Close();
    }
}