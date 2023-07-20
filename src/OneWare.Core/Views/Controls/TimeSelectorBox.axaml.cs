using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DynamicData;

namespace OneWare.Core.Views.Controls
{
    public partial class TimeSelectorBox : UserControl
    {
        public static readonly StyledProperty<bool> RoundInputProperty =
            AvaloniaProperty.Register<TimeSelectorBox, bool>(nameof(RoundInput));
        
        public static readonly StyledProperty<bool> AdjustInputToWaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, bool>(nameof(WaitUnit));
        
        public static readonly StyledProperty<long> FemtoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(FemtoSeconds));
        
        public static readonly StyledProperty<long> PicoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(PicoSeconds));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Label));
        
        public static readonly StyledProperty<double> InputProperty =
            AvaloniaProperty.Register<TimeSelectorBox, double>(nameof(Input));
        
        public static readonly StyledProperty<int> WaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, int>(nameof(WaitUnit));

        public bool RoundInput
        {
            get => GetValue(RoundInputProperty);
            set => SetValue(RoundInputProperty, value);
        }
        
        public bool AdjustInputToWaitUnit
        {
            get => GetValue(AdjustInputToWaitUnitProperty);
            set => SetValue(AdjustInputToWaitUnitProperty, value);
        }
        
        public long FemtoSeconds
        {
            get => GetValue(FemtoSecondsProperty);
            set => SetValue(FemtoSecondsProperty, value);
        }
        
        public long PicoSeconds
        {
            get => GetValue(PicoSecondsProperty);
            set => SetValue(PicoSecondsProperty, value);
        }

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        //0 = ps, 1 = ns, 2 = µs, 3 = ms, 4 = s
        public int WaitUnit
        {
            get => GetValue(WaitUnitProperty);
            set => SetValue(WaitUnitProperty, value);
        }

        public double Input
        {
            get => GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }
        
        public TimeSelectorBox()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == WaitUnitProperty)
            {
                if (AdjustInputToWaitUnit)
                {
                    Input = PicoSeconds / Math.Pow(1000, WaitUnit);
                }
                else
                {
                    CalculateResultFromInput();
                }
            }
            if (change.Property == InputProperty)
            {
                CalculateResultFromInput();
            }
            if (change.Property == FemtoSecondsProperty)
            {
                Input = FemtoSeconds / Math.Pow(1000, WaitUnit) / 1000;
            }
            if (change.Property == PicoSecondsProperty)
            {
                Input = PicoSeconds / Math.Pow(1000, WaitUnit);
            }
            if (RoundInput) Input = Math.Round(Input);
            base.OnPropertyChanged(change);
        }

        private void CalculateResultFromInput()
        {
            PicoSeconds = (long)(Math.Pow(1000, WaitUnit) * Input);
            FemtoSeconds = PicoSeconds * 1000;
        }
    }
}