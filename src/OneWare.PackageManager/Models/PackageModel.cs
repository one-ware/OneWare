using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.PackageManager.Serializer;

namespace OneWare.PackageManager.Models;

public class PackageModel : ObservableObject
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? License { get; init; }
    public IImage? Image { get; init; }
    public List<TabModel>? Tabs { get; init; }
    public List<LinkModel>? Links { get; init; }
    public List<string>? Versions { get; init; }
}