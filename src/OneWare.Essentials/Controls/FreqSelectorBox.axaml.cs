using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using DynamicData.Binding;

namespace OneWare.Essentials.Controls
{
    public partial class FreqSelectorBox : UserControl
    {
        public static readonly StyledProperty<long> HertzProperty =
            AvaloniaProperty.Register<FreqSelectorBox, long>(nameof(Hertz), 0, false, BindingMode.TwoWay);

        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<FreqSelectorBox, string>(nameof(Label));

        private int _freqUnit = 2;

        public FreqSelectorBox()
        {
            InitializeComponent();
        }

        private double _input;
        public double Input
        {
            get => _input;
            set
            {
                _input = value;
                CalculateResult();
            }
        }

        public long Hertz
        {
            get => GetValue(HertzProperty);
            set => SetValue(HertzProperty, value);
        }

        public string Label
        {
            get => GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        //0 = Hz, 1 = KHz, 2 = MHz, 3 = GHz
        public int FreqUnit
        {
            get => _freqUnit;
            set
            {
                _freqUnit = value;
                CalculateResult();
            }
        }



        public void CalculateResult()
        {
            Hertz = (long)(Math.Pow(1000, FreqUnit) * Input);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Input = Hertz / Math.Pow(1000, FreqUnit);
            this.WhenValueChanged(x => x.Hertz).Subscribe(x =>
            {
                InputBox.Text = (Hertz / Math.Pow(1000, FreqUnit)).ToString();
            });
        }
    }
}