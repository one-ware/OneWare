using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.PackageManager.Models;

public class PackageModel : ObservableObject
{
    public string Title { get; }
    
    public string Description { get; }
    
    public IImage Image { get; }

    public PackageModel(string title, string description, IImage image)
    {
        Title = title;
        Description = description;
        Image = image;
    }
}