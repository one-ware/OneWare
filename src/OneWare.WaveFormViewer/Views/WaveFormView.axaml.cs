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
    private double _horizontalScrollDelta = 0;
    private bool _pointerPressed;
    private WaveFormViewModel? _viewModel;
    private double _zoomDelta;
    private readonly PlatformHelper _platformHelper ;

    public WaveFormView(PlatformHelper platformHelper)
    {
        _platformHelper = platformHelper;
        InitializeComponent();

        if (DataContext is WaveFormViewModel viewmodel)
            Initialize(viewmodel);
        else
            DataContextChanged += (o, i) => //WHEN WINDOW IS MOVED Splitscreen etc
            {
                if (DataContext is WaveFormViewModel vm) Initialize(vm);
            };


        // SimPartScroll.WhenAnyValue(x => x.Offset)
        //     .Subscribe(x => TextPartScroll.Offset = TextPartScroll.Offset.WithY(x.Y));
        //
        // TextPartScroll.WhenAnyValue(x => x.Offset)
        //     .Subscribe(x => SimPartScroll.Offset = SimPartScroll.Offset.WithY(x.Y));

        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);

        AddHandler(KeyDownEvent, (sender, args) =>
        {
            if (_viewModel == null) return;

            if (args.Key == Key.Left)
                _viewModel.XOffsetMinus();
            else if (args.Key == Key.Right) _viewModel.XOffsetPlus();
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

            if (args.KeyModifiers == _platformHelper.ControlKey)
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
        _platformHelper = platformHelper;

    }

    private void Initialize(WaveFormViewModel vm)
    {
        _viewModel = vm;
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