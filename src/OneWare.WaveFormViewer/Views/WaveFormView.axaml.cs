using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OneWare.WaveFormViewer.ViewModels;

namespace OneWare.WaveFormViewer.Views
{
    public partial class WaveFormView : UserControl
    {
        private bool _pointerPressed;
        private WaveFormViewModel? _viewModel;
        
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
            

            // SimPartScroll.WhenAnyValue(x => x.Offset)
            //     .Subscribe(x => TextPartScroll.Offset = TextPartScroll.Offset.WithY(x.Y));
            //
            // TextPartScroll.WhenAnyValue(x => x.Offset)
            //     .Subscribe(x => SimPartScroll.Offset = SimPartScroll.Offset.WithY(x.Y));

            AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
            AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Bubble);
            AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Bubble);
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
            SimulatorEffectsRenderer.SetPos(e.GetPosition(SimulatorEffectsRenderer).X, _pointerPressed);
            _pointerPressed = true;
        }

        private void OnPointerReleased(object? sender, PointerEventArgs e)
        {
            _pointerPressed = false;
            SimulatorEffectsRenderer.SetPos(e.GetPosition(SimulatorEffectsRenderer).X, _pointerPressed);
        }
    }
}