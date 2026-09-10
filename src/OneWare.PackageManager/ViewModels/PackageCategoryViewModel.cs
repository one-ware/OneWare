﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Essentials.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageCategoryViewModel(string header, IconModel? iconModel = null) : ObservableObject
{
    public List<PackageViewModel> Packages { get; } = [];

    public ObservableCollection<PackageViewModel> VisiblePackages { get; } = [];

    public ObservableCollection<PackageCategoryViewModel> SubCategories { get; } = [];

    public IconModel? IconModel { get; } = iconModel;

    public string Header { get; } = header;

    public string DisplayName { get; internal set; } = header;

    public void Add(PackageViewModel model)
    {
        Packages.Add(model);
    }

    public void Remove(PackageViewModel model)
    {
        Packages.Remove(model);
        VisiblePackages.Remove(model);
    }

    public void Filter(string filter)
    {
        var filtered =
            Packages.Where(x =>
                x.PackageState.Package.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        foreach (var subCategory in SubCategories)
        {
            subCategory.Filter(filter);
            filtered = filtered.Concat(subCategory.VisiblePackages);
        }

        var orderedPackages = filtered
            .OrderBy(x => x.PackageState.Package.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.PackageState.Package.Id, StringComparer.Ordinal)
            .ToList();

        VisiblePackages.Clear();
        VisiblePackages.AddRange(orderedPackages);
    }
}
