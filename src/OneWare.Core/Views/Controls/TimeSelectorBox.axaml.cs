using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace OneWare.Core.Views.Controls
{
    public partial class TimeSelectorBox : UserControl
    {
        public static readonly StyledProperty<long> PicoSecondsProperty =
            AvaloniaProperty.Register<TimeSelectorBox, long>(nameof(PicoSeconds));

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<TimeSelectorBox, string>(nameof(Label));

        private double _input = 1;

        private int _waitUnit = 3;

        public TimeSelectorBox()
        {
            InitializeComponent();
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
            get => _waitUnit;
            set
            {
                _waitUnit = value;
                CalculateResult();
            }
        }

        public double Input
        {
            get => _input;
            set
            {
                _input = value;
                CalculateResult();
            }
        }



        public void CalculateResult()
        {
            PicoSeconds = (long)(Math.Pow(1000, WaitUnit) * Input);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Input = PicoSeconds / Math.Pow(1000, WaitUnit);
        }
    }
}