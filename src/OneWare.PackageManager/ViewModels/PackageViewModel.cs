using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    public Package Package { get; }
    public IImage? Image { get; init; }
    public List<TabModel>? Tabs { get; init; }
    public List<LinkModel>? Links { get; init; }

    private PackageVersion? _selectedVersion;
    public PackageVersion? SelectedVersion
    {
        get => _selectedVersion;
        set => SetProperty(ref _selectedVersion, value);
    }

    private float _progress;
    public float Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public PackageViewModel(Package package)
    {
        Package = package;
    }
}