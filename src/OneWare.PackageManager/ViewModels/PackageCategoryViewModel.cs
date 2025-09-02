using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Enums;

namespace OneWare.PackageManager.ViewModels;

public class PackageCategoryViewModel(string header, IObservable<object?>? iconObservable = null) : ObservableObject
{
    private bool _isExpanded = true;
    private PackageViewModel? _selectedPackage;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
    public PackageViewModel? SelectedPackage
    {
        get => _selectedPackage;
        set => SetProperty(ref _selectedPackage, value);
    }

    public List<PackageViewModel> Packages { get; } = [];

    public ObservableCollection<PackageViewModel> VisiblePackages { get; } = [];

    public ObservableCollection<PackageCategoryViewModel> SubCategories { get; } = [];

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

    public void Filter(string filter, bool showInstalled, bool showAvailable, bool showUpdate)
    {
        var filtered =
            Packages.Where(x =>
                x.PackageModel.Package.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!showInstalled) filtered = filtered.Where(x => x.PackageModel.Status != PackageStatus.Installed);
        if (!showAvailable) filtered = filtered.Where(x => x.PackageModel.Status != PackageStatus.Available);
        if (!showUpdate) filtered = filtered.Where(x => x.PackageModel.Status != PackageStatus.UpdateAvailable);

        foreach (var subCategory in SubCategories)
        {
            subCategory.Filter(filter, showInstalled, showAvailable, showUpdate);
            filtered = filtered.Concat(subCategory.VisiblePackages);
        }

        VisiblePackages.Clear();
        VisiblePackages.AddRange(filtered.OrderBy(x => x.PackageModel.Package.Name));
    }
}