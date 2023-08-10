using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager.Models;

public class PackageCategoryModel : ObservableObject
{
    public ObservableCollection<PackageViewModel> Packages { get; } = new ();
    public IObservable<object?>? IconObservable { get; }
    
    public string Header { get; }

    public PackageCategoryModel(string header, IObservable<object?>? iconObservable = null)
    {
        Header = header;
        IconObservable = iconObservable;
    }
}