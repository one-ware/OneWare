using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.PackageManager.Models;

public class PackageCategoryModel : ObservableObject
{
    public ObservableCollection<PackageModel> Packages { get; } = new ();
    public IObservable<object?>? IconObservable { get; }
    
    public string Header { get; }

    public PackageCategoryModel(string header, IObservable<object?>? iconObservable = null)
    {
        Header = header;
        IconObservable = iconObservable;
    }
}