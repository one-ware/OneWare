using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Essentials.Models;

public class IconModel : ObservableObject
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

public class IconLayer
{
    public IconLayer()
    {
    }

    public IconLayer(string resourceKey)
    {
        IconObservable = Application.Current!.GetResourceObservable(resourceKey)!.Cast<IImage>();
    }

    public IImage? Icon { get; init; }

    public IObservable<IImage?>? IconObservable { get; init; }

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Right;

    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Bottom;

    public Thickness Margin { get; set; } = new(0);

    public double? Size { get; set; }

    public double SizeRatio { get; set; } = 0.5;
}
