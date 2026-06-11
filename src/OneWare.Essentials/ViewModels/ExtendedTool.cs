using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Dock.Controls.DeferredContentControl;
using Dock.Model.Mvvm.Controls;
using OneWare.Essentials.Models;

namespace OneWare.Essentials.ViewModels;

public abstract class ExtendedTool : Tool, IExtendedTool, IDeferredContentPresentation
{
    private IImage? _icon;

    protected ExtendedTool(string iconKey)
    {
        Application.Current?.GetResourceObservable(iconKey).Subscribe(x => { Icon = x as IImage; });
        KeepPinnedDockableVisible = true;
    }

    protected ExtendedTool(IImage icon)
    {
        _icon = icon;
    }

    public IImage? Icon
    {
        get => _icon;
        private set => SetProperty(ref _icon, value);
    }


    public virtual void InitializeContent()
    {
    }

    public bool DeferContentPresentation => false;
}