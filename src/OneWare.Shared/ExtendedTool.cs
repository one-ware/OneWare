using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;

namespace OneWare.Shared;

public abstract class ExtendedTool : Tool, IExtendedTool
{
    public bool IsContentInitialized { get; private set; }
    
    private IImage? _icon;
    public IImage? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }

    protected ExtendedTool(string iconKey)
    {
        Application.Current?.GetResourceObservable(iconKey).Subscribe(x =>
        {
            Icon = x as IImage;
        });
    }

    protected ExtendedTool(IImage icon)
    {
        _icon = icon;
    }


    public virtual void InitializeContent()
    {
        IsContentInitialized = true;
    }
}