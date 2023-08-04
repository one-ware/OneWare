using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Dock.Model.Mvvm.Controls;
using OneWare.Shared.Enums;
using OneWare.Shared.Services;
using Prism.Ioc;

namespace OneWare.Shared.Controls
{
    public class FlexibleWindow : UserControl
    {
        public static readonly StyledProperty<double> PrefWidthProperty =
            AvaloniaProperty.Register<FlexibleWindow, double>(nameof(PrefWidth), double.NaN);
        
        public static readonly StyledProperty<double> PrefHeightProperty =
            AvaloniaProperty.Register<FlexibleWindow, double>(nameof(PrefHeight), double.NaN);
        
        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<FlexibleWindow, bool>(nameof(ShowTitleProperty), true);
        
        public static readonly StyledProperty<IImage?> CustomIconProperty =
            AvaloniaProperty.Register<FlexibleWindow, IImage?>(nameof(CustomIcon));

        public static readonly StyledProperty<Control?> TitleBarContentProperty =
            AvaloniaProperty.Register<FlexibleWindow, Control?>(nameof(TitleBarContent));
        
        public static readonly StyledProperty<Control?> BottomContentProperty =
            AvaloniaProperty.Register<FlexibleWindow, Control?>(nameof(BottomContent));
        
        public static readonly StyledProperty<WindowStartupLocation> WindowStartupLocationProperty =
            AvaloniaProperty.Register<FlexibleWindow, WindowStartupLocation>(nameof(WindowStartupLocation));
        
        public static readonly StyledProperty<WindowIcon?> IconProperty =
            AvaloniaProperty.Register<FlexibleWindow, WindowIcon?>(nameof(Icon));
        
        public static readonly StyledProperty<string?> TitleProperty =
            AvaloniaProperty.Register<FlexibleWindow, string?>(nameof(Title), "Window");
        
        public static readonly StyledProperty<SizeToContent> SizeToContentProperty =
            AvaloniaProperty.Register<FlexibleWindow, SizeToContent>(nameof(SizeToContent));
        
        public static readonly StyledProperty<IBrush?> WindowBackgroundProperty =
            AvaloniaProperty.Register<FlexibleWindow, IBrush?>(nameof(WindowBackground));
        
        public static readonly StyledProperty<IReadOnlyList<WindowTransparencyLevel>> TransparencyLevelHintProperty =
            AvaloniaProperty.Register<FlexibleWindow, IReadOnlyList<WindowTransparencyLevel>>(nameof(TransparencyLevelHint), Array.Empty<WindowTransparencyLevel>());

        public double PrefWidth
        {
            get => GetValue(PrefWidthProperty);
            set => SetValue(PrefWidthProperty, value);
        }
        
        public double PrefHeight
        {
            get => GetValue(PrefHeightProperty);
            set => SetValue(PrefHeightProperty, value);
        }
        
        public bool ShowTitle
        {
            get => GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        public IImage? CustomIcon
        {
            get => GetValue(CustomIconProperty);
            set => SetValue(CustomIconProperty, value);
        }

        public Control? TitleBarContent
        {
            get => GetValue(TitleBarContentProperty);
            set => SetValue(TitleBarContentProperty, value);
        }
        
        public Control? BottomContent
        {
            get => GetValue(BottomContentProperty);
            set => SetValue(BottomContentProperty, value);
        }

        public WindowStartupLocation WindowStartupLocation
        {
            get => GetValue(WindowStartupLocationProperty);
            set => SetValue(WindowStartupLocationProperty, value);
        }
        
        public WindowIcon? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        
        public string? Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public SizeToContent SizeToContent
        {
            get => GetValue(SizeToContentProperty);
            set => SetValue(SizeToContentProperty, value);
        }
        
        public IBrush? WindowBackground
        {
            get => GetValue(WindowBackgroundProperty);
            set => SetValue(WindowBackgroundProperty, value);
        }
        
        public IReadOnlyList<WindowTransparencyLevel> TransparencyLevelHint
        {
            get => GetValue(TransparencyLevelHintProperty);
            set => SetValue(TransparencyLevelHintProperty, value);
        }
        
        public event EventHandler? Opened;
        
        public event EventHandler? Closed;
        
        public Window? Host { get; private set; }
        
        public void Show(Window? owner)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                Host = ContainerLocator.Container.Resolve<IWindowService>().CreateHost(this);
                Host.Opened += (sender, args) => Opened?.Invoke(sender, args);
                if (owner != null) Host.Show(owner);
                else Host.Show();
            }
            else
            {
                if (DataContext is not Document doc)
                    throw new Exception("ViewModel for FlexibleWindow must be Document");

                ContainerLocator.Container.Resolve<IDockService>().Show(doc, DockShowLocation.Document);
            }
        }

        public Task ShowDialogAsync(Window owner)
        {
            if (owner == null) throw new NullReferenceException("Owner is needed on Classic Desktop Environment");
            Host = ContainerLocator.Container.Resolve<IWindowService>().CreateHost(this);
            Host.Opened += (sender, args) => Opened?.Invoke(sender, args);
            return Host.ShowDialog(owner);
        }

        public void Close()
        {
            Host?.Close();    
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}