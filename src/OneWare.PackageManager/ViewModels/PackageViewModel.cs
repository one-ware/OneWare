using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Models;
using OneWare.PackageManager.Serializer;

namespace OneWare.PackageManager.ViewModels;

public class PackageViewModel : ObservableObject
{
    public Package Package { get; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? License { get; init; }
    public IImage? Image { get; init; }
    public List<TabModel>? Tabs { get; init; }
    public List<LinkModel>? Links { get; init; }
    public List<string>? Versions { get; init; }

    private string? _selectedVersion;
    public string? SelectedVersion
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