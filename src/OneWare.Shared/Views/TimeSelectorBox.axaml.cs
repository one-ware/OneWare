using System.Numerics;
using Avalonia;
using Avalonia.Controls;

namespace OneWare.Shared.Views
{
    public partial class TimeSelectorBox : UserControl
    {
        public static readonly StyledProperty<bool> AdjustInputToWaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, bool>(nameof(WaitUnit));
        
        public static readonly StyledProperty<long> FemtoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(FemtoSeconds));
        
        public static readonly StyledProperty<long> PicoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(PicoSeconds));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Label));
        
        public static readonly StyledProperty<long> InputProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(Input));
        
        public static readonly StyledProperty<int> WaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, int>(nameof(WaitUnit));

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

        public long Input
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
                    Input = PicoSeconds / (long)BigInteger.Pow(1000, WaitUnit);
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
                Input = FemtoSeconds / (long)BigInteger.Pow(1000, WaitUnit) / 1000;
            }
            if (change.Property == PicoSecondsProperty)
            {
                Input = PicoSeconds / (long)BigInteger.Pow(1000, WaitUnit);
            }
            base.OnPropertyChanged(change);
        }

        private void CalculateResultFromInput()
        {
            PicoSeconds = (long)(BigInteger.Pow(1000, WaitUnit) * Input);
            FemtoSeconds = PicoSeconds * 1000;
        }
    }
}