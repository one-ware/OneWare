using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Services;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Views;

public partial class WaveFormView : UserControl
{
    private const double DragThreshold = 3;

    private bool _pointerPressed;
    private bool _dragging;
    private double _pressedX;
    private WaveFormViewModel? _viewModel;
    private double _zoomDelta;

    private ScrollViewer? _textScrollViewer;
    private ScrollViewer? _simScrollViewer;
    private bool _syncingScrollOffset;

    public WaveFormView()
    {
        InitializeComponent();

        if (DataContext is WaveFormViewModel viewmodel)
            Initialize(viewmodel);
        else
            DataContextChanged += (o, i) => //WHEN WINDOW IS MOVED Splitscreen etc
            {
                if (DataContext is WaveFormViewModel vm) Initialize(vm);
            };

        Loaded += OnLoaded;

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);

        AddHandler(KeyDownEvent, OnKeyDown);

        AddHandler(PointerWheelChangedEvent, (sender, args) =>
        {
            if (_viewModel == null) return;

            if (args.KeyModifiers is KeyModifiers.Shift)
            {
                if (args.Delta.Y != 0)
                {
                    if (args.Delta.Y < 0)
                        _viewModel.XOffsetMinus();
                    else if (args.Delta.Y > 0) _viewModel.XOffsetPlus();

                    args.Handled = true;
                }

                return;
            }

            if (args.KeyModifiers == PlatformHelper.ControlKey)
            {
                if (args.Delta.Y != 0)
                {
                    _zoomDelta += args.Delta.Y;

                    if (_zoomDelta < -1)
                    {
                        _viewModel.ZoomOut();
                        _zoomDelta = 0;
                    }
                    else if (args.Delta.Y > 0)
                    {
                        _viewModel.ZoomIn();
                        _zoomDelta = 0;
                    }

                    args.Handled = true;
                }

                return;
            }

            _zoomDelta = 0;

            if (args.Delta.X != 0)
            {
                var plus = (long)(_viewModel.Max / _viewModel.ZoomMultiply / 10 * args.Delta.X * -1);
                _viewModel.Offset += plus;
                args.Handled = true;
            }
            //else if(args.Delta.Y == 1) _viewModel.ZoomIn();
            //else if(args.Delta.Y == -1) _viewModel.ZoomOut();
        });
    }

    private void Initialize(WaveFormViewModel vm)
    {
        _viewModel = vm;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_textScrollViewer != null && _simScrollViewer != null)
            return;

        _textScrollViewer = TextPartScroll.FindDescendantOfType<ScrollViewer>();
        _simScrollViewer = SimPartScroll.FindDescendantOfType<ScrollViewer>();

        if (_textScrollViewer == null || _simScrollViewer == null)
            return;

        _textScrollViewer.ScrollChanged += (_, _) =>
            SyncScrollOffset(_simScrollViewer, _textScrollViewer.Offset);
        _simScrollViewer.ScrollChanged += (_, _) =>
            SyncScrollOffset(_textScrollViewer, _simScrollViewer.Offset);
    }

    private void SyncScrollOffset(ScrollViewer target, Vector source)
    {
        if (_syncingScrollOffset)
            return;

        if (Math.Abs(target.Offset.Y - source.Y) < 0.5)
            return;

        _syncingScrollOffset = true;
        target.Offset = target.Offset.WithY(source.Y);
        _syncingScrollOffset = false;
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (_viewModel == null) return;

        var modifiers = args.KeyModifiers;

        if (args.Key == Key.Left && modifiers == PlatformHelper.ControlKey)
            _viewModel.JumpToPreviousEdge();
        else if (args.Key == Key.Right && modifiers == PlatformHelper.ControlKey)
            _viewModel.JumpToNextEdge();
        else if (args.Key == Key.Left)
            _viewModel.XOffsetMinus();
        else if (args.Key == Key.Right)
            _viewModel.XOffsetPlus();
        else if (args.Key == Key.Delete && modifiers == KeyModifiers.None && _viewModel.SelectedSignals.Count > 0)
            _viewModel.RemoveSignals(null);
        else if (args.Key == Key.Escape && _viewModel.HasSecondMarker)
            _viewModel.ClearSecondMarker();
        else
            return;

        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_viewModel == null) return;

        var x = e.GetPosition(SimulatorEffectsRenderer).X;
        var offset = SimulatorEffectsRenderer.GetOffsetFromPosition(x);
        _viewModel.CursorOffset = offset;

        if (!_pointerPressed || (!_dragging && Math.Abs(x - _pressedX) < DragThreshold)) return;

        // Dragging measures from the marker to the pointer. The second marker stays after release.
        _dragging = true;
        _viewModel.SecondMarkerOffset = offset;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel == null) return;
        if ((e.Source as Visual).FindAncestorOfType<ListBox>() is not { Name: "SimPartScroll" }) return;

        var point = e.GetCurrentPoint(SimulatorEffectsRenderer);
        var offset = SimulatorEffectsRenderer.GetOffsetFromPosition(point.Position.X);

        if (point.Properties.IsRightButtonPressed ||
            (point.Properties.IsLeftButtonPressed && e.KeyModifiers.HasFlag(KeyModifiers.Shift)))
        {
            _viewModel.SecondMarkerOffset = offset;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed) return;

        _viewModel.MarkerOffset = offset;
        _pointerPressed = true;
        _dragging = false;
        _pressedX = point.Position.X;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pointerPressed = false;
        _dragging = false;
    }

    private void ExportImage_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = ExportImageAsync(true);
    }

    private void ExportImageWithoutMarkers_OnClick(object? sender, RoutedEventArgs e)
    {
        _ = ExportImageAsync(false);
    }

    private async Task ExportImageAsync(bool includeMarkers)
    {
        if (TopLevel.GetTopLevel(this) is not { } topLevel) return;

        var path = await StorageProviderHelper.SelectSaveFileAsync(topLevel, "Export Waveform", null, ".png",
            "waveform.png", true, new FilePickerFileType("PNG Image") { Patterns = ["*.png"] });

        if (path == null) return;

        try
        {
            using var bitmap = RenderImage(includeMarkers);
            bitmap?.Save(path);
        }
        catch (Exception exception)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(exception.Message, exception);
        }
    }

    /// <summary>
    ///     Renders the visible scale, signal names and waves (without toolbar and scrollbar) to a bitmap.
    /// </summary>
    public Bitmap? RenderImage(bool includeMarkers)
    {
        var area = SimulatorScaleRenderer.Bounds;
        if (area.Width < 1 || area.Height < 1) return null;

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var fullSize = PixelSize.FromSize(TopPartGrid.Bounds.Size, scaling);
        var cropRect = PixelRect.FromRect(area, scaling).Intersect(new PixelRect(fullSize));

        var effectsVisible = SimulatorEffectsRenderer.IsVisible;
        SimulatorEffectsRenderer.IsVisible = effectsVisible && includeMarkers;

        try
        {
            using var fullImage = new RenderTargetBitmap(fullSize, new Vector(96 * scaling, 96 * scaling));
            fullImage.Render(TopPartGrid);

            var image = new WriteableBitmap(cropRect.Size, fullImage.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var buffer = image.Lock();
            fullImage.CopyPixels(cropRect, buffer.Address, buffer.RowBytes * cropRect.Height, buffer.RowBytes);
            return image;
        }
        finally
        {
            SimulatorEffectsRenderer.IsVisible = effectsVisible;
        }
    }
}