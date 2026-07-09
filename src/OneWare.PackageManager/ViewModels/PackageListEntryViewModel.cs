using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.PackageManager.ViewModels;

public abstract class PackageListEntryViewModel : ObservableObject
{
    public virtual bool IsSelectable => true;
}
