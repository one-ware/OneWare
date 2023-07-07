using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.PackageManager.Models;

public class PackageModel : ObservableObject
{
    public string Title { get; }
    
    public string ShortDescription { get; }
    
    public string Description { get; }
    
    public List<LinkModel> Links { get; }
    
    public IImage? Image { get; }
    
    public PackageModel(string title, string shortDescription, string description, IImage? image, List<LinkModel> links)
    {
        Title = title;
        ShortDescription = shortDescription;
        Description = description;
        Image = image;
        Links = links;
    }
}