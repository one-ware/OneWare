using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageCategoryViewModel(string header, IObservable<object?>? iconObservable = null) : ObservableObject
{
    public List<PackageViewModel> Packages { get; } = [];
    
    public ObservableCollection<PackageViewModel> VisiblePackages { get; } = [];
    
    public IObservable<object?>? IconObservable { get; } = iconObservable;
    
    public string Header { get; } = header;

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
            Packages.Where(x => x.PackageModel.Package.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!showInstalled) filtered = filtered.Where(x => x.PackageModel.Status != PackageStatus.Installed);
        if (!showAvailable) filtered = filtered.Where(x => x.PackageModel.Status != PackageStatus.Available);

        VisiblePackages.Clear();
        VisiblePackages.AddRange(filtered.OrderBy(x => x.PackageModel is NativeToolPackageModel).ThenBy(x => x.PackageModel.Package.Name));
    }
}