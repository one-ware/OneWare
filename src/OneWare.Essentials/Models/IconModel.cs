using System.Collections.Generic;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;

namespace OneWare.Essentials.Models;

public class IconModel : ObservableObject
{
    private readonly Dictionary<string, IconLayer> _iconLayers = new();
    
    public IconModel()
    {
        
    }
    
    public IconModel(string resourceKey)
    {
        IconObservable = Application.Current!.GetResourceObservable(resourceKey)!.Cast<IImage>();
    }
    
    public IImage? Icon { get; init; }
    
    public IObservable<IImage?>? IconObservable { get; init; }

    public IconLayer[] Overlays => _iconLayers.Values.ToArray();

    public void AddOverlay(string key, string resourceKey)
    {
        if(_iconLayers.ContainsKey(key)) return;
        _iconLayers.Add(key, new IconLayer(resourceKey));
        OnPropertyChanged(nameof(Overlays));
    }
    
    public void RemoveOverlay(string key)
    {
        if (_iconLayers.Remove(key))
        {
            OnPropertyChanged(nameof(Overlays));
        }
    }
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

    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;

    public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;

    public Thickness Margin { get; set; } = new(0);

    public double? Size { get; set; }

    public double SizeRatio { get; set; } = 1;
}
