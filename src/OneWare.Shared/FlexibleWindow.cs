using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Dock.Model.Core;
using static System.Double;

namespace OneWare.Shared
{
    public class FlexibleWindow : UserControl
    {
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
        
        public static readonly StyledProperty<IReadOnlyList<WindowTransparencyLevel>> TransparencyLevelHintProperty =
            AvaloniaProperty.Register<FlexibleWindow, IReadOnlyList<WindowTransparencyLevel>>(nameof(TransparencyLevelHint), Array.Empty<WindowTransparencyLevel>());

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
        
        public IReadOnlyList<WindowTransparencyLevel> TransparencyLevelHint
        {
            get => GetValue(TransparencyLevelHintProperty);
            set => SetValue(TransparencyLevelHintProperty, value);
        }
        
        public event EventHandler? Opened;
        
        public event EventHandler? Closed;
        
        public AdvancedWindow? Host { get; private set; }

        public FlexibleWindow()
        {
            
        }

        public void Show(Window owner)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                Host = CreateHost();
                Host.Show(owner);
                
                Observable.FromEventPattern<TemplateAppliedEventArgs>(h => Host.TemplateApplied += h, h => Host.TemplateApplied -= h)
                    .Take(1)
                    .Subscribe((x) =>
                    {
                        Height = NaN;
                        Width = NaN;
                    });
            }
            else
            {
                
            }
        }

        public Task ShowDialogAsync(Window owner)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime)
            {
                Host = CreateHost();
                return Host.ShowDialog(owner);
            }
            else
            {
                
            }
            
            return Task.CompletedTask;
        }

        public void Close()
        {
            Host?.Close();    
        }
        
        private AdvancedWindow CreateHost()
        {
            var host = new AdvancedWindow();
            
            host.Bind(AdvancedWindow.ShowTitleProperty, this.GetObservable(ShowTitleProperty));
            host.Bind(AdvancedWindow.CustomIconProperty, this.GetObservable(CustomIconProperty));
            host.Bind(AdvancedWindow.TitleBarContentProperty, this.GetObservable(TitleBarContentProperty));
            host.Bind(AdvancedWindow.BottomContentProperty, this.GetObservable(BottomContentProperty));
            
            host.Bind(Window.WindowStartupLocationProperty, this.GetObservable(WindowStartupLocationProperty));
            host.Bind(Window.IconProperty, this.GetObservable(IconProperty));
            host.Bind(Window.TitleProperty, this.GetObservable(TitleProperty));
            host.Bind(Window.SizeToContentProperty, this.GetObservable(SizeToContentProperty));
            
            host.Bind(TopLevel.TransparencyLevelHintProperty, this.GetObservable(TransparencyLevelHintProperty));

            host.Height = Height;
            host.Width = Width;

            host.ExtendClientAreaToDecorationsHint = true;
            host.SizeToContent = SizeToContent.WidthAndHeight;

            host.Opened += (sender, args) => Opened?.Invoke(sender, args);
            host.Closed += (sender, args) => Closed?.Invoke(sender, args);

            host.Content = this;

            return host;
        }
    }
}