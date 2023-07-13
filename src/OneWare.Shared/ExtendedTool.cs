using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;

namespace OneWare.Shared;

public class ExtendedTool : Tool, IExtendedTool
{
    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public ExtendedTool(string iconKey)
    {
        Application.Current?.GetResourceObservable(iconKey).Subscribe(x =>
        {
            Icon = x as IImage;
        });
    }
    
    public ExtendedTool(IImage icon)
    {
        _icon = icon;
    }
}