﻿using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageCategoryViewModel(string header, IconModel? iconModel = null) : ObservableObject
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

    public ObservableCollection<object> VisibleEntries { get; } = [];

    public ObservableCollection<PackageCategoryViewModel> SubCategories { get; } = [];

    public IconModel? IconModel { get; } = iconModel;

    public string Header { get; } = header;

    public void Add(PackageViewModel model)
    {
        Packages.Add(model);
    }

    public void Remove(PackageViewModel model)
    {
        Packages.Remove(model);
        VisiblePackages.Remove(model);
        VisibleEntries.Remove(model);
    }

    public void Filter(string filter, bool showInstalled, bool showAvailable)
    {
        var filtered =
            Packages.Where(x =>
                x.PackageState.Package.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        if (!showInstalled)
            filtered = filtered.Where(x => !IsInstalledPackage(x.PackageState.Status));

        if (!showAvailable)
            filtered = filtered.Where(x => IsInstalledPackage(x.PackageState.Status));

        foreach (var subCategory in SubCategories)
        {
            subCategory.Filter(filter, showInstalled, showAvailable);
            filtered = filtered.Concat(subCategory.VisiblePackages);
        }

        var orderedPackages = filtered
            .OrderBy(GetPackageGroupPriority)
            .ThenBy(x => x.PackageState.Package.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        VisiblePackages.Clear();
        VisiblePackages.AddRange(orderedPackages);

        VisibleEntries.Clear();
        VisibleEntries.AddRange(CreateVisibleEntries(orderedPackages));
    }

    private static int GetPackageGroupPriority(PackageViewModel package)
    {
        return package.PackageState.Status switch
        {
            PackageStatus.UpdateAvailable => 0,
            PackageStatus.UpdateAvailablePrerelease => 0,
            PackageStatus.Installed => 1,
            PackageStatus.NeedRestart => 1,
            PackageStatus.Installing => 1,
            _ => 2
        };
    }

    private static bool IsInstalledPackage(PackageStatus status)
    {
        return status is PackageStatus.Installed
            or PackageStatus.UpdateAvailable
            or PackageStatus.UpdateAvailablePrerelease
            or PackageStatus.NeedRestart
            or PackageStatus.Installing;
    }

    private static IReadOnlyList<object> CreateVisibleEntries(IReadOnlyList<PackageViewModel> packages)
    {
        var groups = packages
            .GroupBy(GetPackageGroupPriority)
            .OrderBy(x => x.Key)
            .ToList();

        if (groups.Count <= 1)
            return packages.Cast<object>().ToList();

        var entries = new List<object>(packages.Count + groups.Count);

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            var group = groups[groupIndex];
            entries.Add(new PackageSeparatorViewModel(GetGroupLabel(group.Key), groupIndex > 0));

            foreach (var package in group)
                entries.Add(package);
        }

        return entries;
    }

    private static string GetGroupLabel(int groupPriority)
    {
        return groupPriority switch
        {
            0 => "Update Available",
            1 => "Installed",
            _ => "Available"
        };
    }
}
