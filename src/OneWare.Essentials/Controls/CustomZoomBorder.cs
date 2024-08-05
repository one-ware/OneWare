using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;

namespace OneWare.Essentials.Controls;

public class CustomZoomBorder : ZoomBorder
{
    private const int KeyPanSpeed = 5;
    
    protected override Type StyleKeyOverride => typeof(ZoomBorder);
    
    private CompositeDisposable _disposables = new();

    private readonly List<Key> _keysDown = [];

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        PointerEntered += (_, _) =>
        {
            this.Focus();
        };
            
        DispatcherTimer.Run(() =>
            {
                if (_keysDown.Contains(Key.Down) || _keysDown.Contains(Key.S))
                    PanDelta(0, KeyPanSpeed);
                else if (_keysDown.Contains(Key.Up) || _keysDown.Contains(Key.W))
                    PanDelta(0, KeyPanSpeed * -1);
                if (_keysDown.Contains(Key.Left) || _keysDown.Contains(Key.A))
                    PanDelta(KeyPanSpeed * -1, 0);
                else if (_keysDown.Contains(Key.Right) || _keysDown.Contains(Key.D))
                    PanDelta(KeyPanSpeed, 0);

                return true;
            }, TimeSpan.FromMilliseconds(10))
            .DisposeWith(_disposables);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _disposables.Dispose();
        _disposables = new CompositeDisposable();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        _keysDown.Add(e.Key);
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _keysDown.RemoveAll(x => x == e.Key);
        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        _keysDown.Clear();
        base.OnLostFocus(e);
    }
}