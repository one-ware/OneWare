using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace OneWare.Essentials.Models;

public class IconModel
{
    public IconModel()
    {
        
    }
    
    public IconModel(string resourceKey)
    {
        IconObservable = Application.Current!.GetResourceObservable(resourceKey)!.Cast<IImage>();
    }
    
    public IImage? Icon { get; init; }
    
    public IObservable<IImage?>? IconObservable { get; init; }
}
