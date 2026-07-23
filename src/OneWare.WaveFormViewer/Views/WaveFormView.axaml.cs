using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OneWare.Essentials.Helpers;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Views;

public partial class WaveFormView : UserControl
{
    private bool _pointerPressed;
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

        AddHandler(KeyDownEvent, (sender, args) =>
        {
            if (_viewModel == null) return;

            if (args.Key == Key.Left)
            {
                if (args.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    _viewModel.JumpToPreviousEdge();
                    args.Handled = true;
                }
                else
                {
                    _viewModel.XOffsetMinus();
                }
            }
            else if (args.Key == Key.Right)
            {
                if (args.KeyModifiers.HasFlag(KeyModifiers.Alt))
                {
                    _viewModel.JumpToNextEdge();
                    args.Handled = true;
                }
                else
                {
                    _viewModel.XOffsetPlus();
                }
            }
            else if (args.Key == Key.Escape)
            {
                _viewModel.ClearMarkers();
                args.Handled = true;
            }
        });

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

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        SimulatorEffectsRenderer.SetPos(e.GetPosition(SimulatorEffectsRenderer).X, _pointerPressed,
            !_pointerPressed);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if ((e.Source as Visual).FindAncestorOfType<ListBox>() is not { Name: "SimPartScroll" }) return;
        SimulatorEffectsRenderer.SetPos(e.GetPosition(SimulatorEffectsRenderer).X, _pointerPressed);
        _pointerPressed = true;
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if ((e.Source as Visual).FindAncestorOfType<ListBox>() is not { Name: "SimPartScroll" }) return;
        _pointerPressed = false;
        SimulatorEffectsRenderer.SetPos(e.GetPosition(SimulatorEffectsRenderer).X, _pointerPressed);
    }
}