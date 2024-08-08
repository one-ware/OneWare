using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        
        KeyDownEvent.AddClassHandler<TopLevel>((sender, args) =>
        {
            if(IsPointerOver) _keysDown.Add(args.Key);
        }, handledEventsToo: true).DisposeWith(_disposables);
        
        KeyUpEvent.AddClassHandler<TopLevel>((sender, args) =>
        {
            _keysDown.RemoveAll(x => x == args.Key);
        }, handledEventsToo: true).DisposeWith(_disposables);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _disposables.Dispose();
        _disposables = new CompositeDisposable();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _keysDown.Clear();
        base.OnPointerExited(e);
    }

    public void AutoFit()
    {
        AutoFit(false);
    }
}