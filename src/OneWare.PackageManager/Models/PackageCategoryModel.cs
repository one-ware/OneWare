using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Enums;
using OneWare.PackageManager.ViewModels;

namespace OneWare.PackageManager.Models;

public class PackageCategoryModel : ObservableObject
{
    public List<PackageViewModel> Packages { get; } = [];
    
    public ObservableCollection<PackageViewModel> VisiblePackages { get; } = [];
    
    public IObservable<object?>? IconObservable { get; }
    public string Header { get; }

    public PackageCategoryModel(string header, IObservable<object?>? iconObservable = null)
    {
        Header = header;
        IconObservable = iconObservable;
    }

    public void Add(PackageViewModel model)
    {
        Packages.Add(model);
    }

    public void Remove(PackageViewModel model)
    {
        Packages.Remove(model);
        VisiblePackages.Remove(model);
    }

    public void Filter(string filter, bool showInstalled, bool showAvailable)
    {
        var filtered =
            Packages.Where(x => x.Package.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!showInstalled) filtered = filtered.Where(x => x.Status != PackageStatus.Installed);
        if (!showAvailable) filtered = filtered.Where(x => x.Status != PackageStatus.Available);

        VisiblePackages.Clear();
        VisiblePackages.AddRange(filtered.OrderBy(x => x.Package.Name));
    }
}