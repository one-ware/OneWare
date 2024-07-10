using System.Numerics;
using Avalonia;
using Avalonia.Controls;

namespace OneWare.Essentials.Controls
{
    public partial class TimeSelectorBox : UserControl
    {
        public static readonly StyledProperty<bool> AdjustDisplayToWaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, bool>(nameof(WaitUnit));
        
        public static readonly StyledProperty<long> FemtoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(FemtoSeconds));
        
        public static readonly StyledProperty<long> PicoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(PicoSeconds));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Label));
        
        public static readonly StyledProperty<long> InOutProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(InOut));
        
        public static readonly StyledProperty<int> WaitUnitProperty =
            AvaloniaProperty.Register<TimeSelectorBox, int>(nameof(WaitUnit));
        
        public static readonly StyledProperty<long> DisplayProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(PicoSeconds));
        
        public static readonly StyledProperty<long> InOutTimeScaleProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(InOutTimeScale));

        public bool AdjustDisplayToWaitUnit
        {
            get => GetValue(AdjustDisplayToWaitUnitProperty);
            set => SetValue(AdjustDisplayToWaitUnitProperty, value);
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

        //1 = fs, 1000 = ps, 1000_000 = ns, 1000_000_000 = μs, 1000_000_000_000 = ms, 1000_000_000_000_000 = s
        public long InOutTimeScale
        {
            get => GetValue(InOutTimeScaleProperty);
            set => SetValue(InOutTimeScaleProperty, value);
        }
        
        public long InOut
        {
            get => GetValue(InOutProperty);
            set => SetValue(InOutProperty, value);
        }
        
        public long Display
        {
            get => GetValue(DisplayProperty);
            set => SetValue(DisplayProperty, value);
        }
        
        public TimeSelectorBox()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == WaitUnitProperty)
            {
                if (AdjustDisplayToWaitUnit)
                {
                    Display = PicoSeconds / (long)BigInteger.Pow(1000, WaitUnit);
                }
                else
                {
                    CalculateResultFromDisplay();
                }
            }
            if (change.Property == DisplayProperty)
            {
                CalculateResultFromDisplay();
            }
            if (change.Property == InOutProperty)
            {
                FemtoSeconds = InOut * InOutTimeScale;
                PicoSeconds = FemtoSeconds / 1000;
                Display = PicoSeconds / (long)BigInteger.Pow(1000, WaitUnit);
            }
            base.OnPropertyChanged(change);
        }

        private void CalculateResultFromDisplay()
        {
            PicoSeconds = (long)(BigInteger.Pow(1000, WaitUnit) * Display);
            FemtoSeconds = PicoSeconds * 1000;
            InOut = FemtoSeconds / InOutTimeScale;
        }
    }
}