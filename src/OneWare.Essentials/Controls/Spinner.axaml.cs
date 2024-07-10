using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace OneWare.Essentials.Controls
{
    public partial class Spinner : UserControl
    {
        public static readonly StyledProperty<IImage?> CustomIconProperty =
            AvaloniaProperty.Register<Spinner, IImage?>(nameof(CustomIcon));

        public static readonly StyledProperty<bool> IsIntermediateProperty =
            AvaloniaProperty.Register<Spinner, bool>(nameof(IsIntermediate), true);

        public static readonly StyledProperty<bool> IsAnimationRunningProperty =
            AvaloniaProperty.Register<Spinner, bool>(nameof(IsAnimationRunning), true);

        public Spinner()
        {
            InitializeComponent();
            PropertyChanged += (o, i) =>
            {
                if (i.Property == IsAnimationRunningProperty || i.Property == IsVisibleProperty) Update();
            };
        }

        public bool IsIntermediate
        {
            get => GetValue(IsIntermediateProperty);
            set => SetValue(IsIntermediateProperty, value);
        }

        public bool IsAnimationRunning
        {
            get => GetValue(IsAnimationRunningProperty);
            set => SetValue(IsAnimationRunningProperty, value);
        }

        public IImage? CustomIcon
        {
            get => GetValue(CustomIconProperty);
            set => SetValue(CustomIconProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            CustomIcon ??= this.FindResource( Application.Current?.RequestedThemeVariant, "Spinner") as DrawingImage;
        }
        

        private void Update()
        {
            SpinnerPresenter.Classes.Clear();
            if (IsAnimationRunning && IsVisible)
            {
                if (IsIntermediate) SpinnerPresenter.Classes.Add("AnimatingIntermediate");
                else SpinnerPresenter.Classes.Add("AnimatingOnce");
            }
        }
    }
}