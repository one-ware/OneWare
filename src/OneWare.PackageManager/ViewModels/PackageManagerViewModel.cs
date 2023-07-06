using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Models;

namespace OneWare.PackageManager.ViewModels;

public class PackageManagerViewModel : ObservableObject
{
    private PackageCategoryModel? _selectedCategory;

    public PackageCategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }
    
    public ObservableCollection<PackageCategoryModel> PackageCategories { get; } = new();

    public PackageManagerViewModel()
    {
        PackageCategories.Add(new PackageCategoryModel("Languages", Application.Current?.GetResourceObservable("FluentIcons.ProofreadLanguageRegular")));
        PackageCategories.Add(new PackageCategoryModel("Toolchains", Application.Current?.GetResourceObservable("FeatherIcons.Tool")));
        PackageCategories.Add(new PackageCategoryModel("Boards", Application.Current?.GetResourceObservable("NiosIcon"))
        {
            Packages =
            {
                new PackageModel("Max1000", "The MAX1000 FPGA Development Board is the most inexpensive way to start with FPGAs and OneWare Studio", null)
            }
        });
        PackageCategories.Add(new PackageCategoryModel("Libraries", Application.Current?.GetResourceObservable("BoxIcons.RegularLibrary")));
        SelectedCategory = PackageCategories.First();
    }
}