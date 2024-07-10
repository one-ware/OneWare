using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using OneWare.Essentials.ViewModels;

namespace OneWare.Essentials.Controls
{
    public class AdvancedWindow : Window
    {
        public static readonly StyledProperty<bool> ShowTitleProperty =
            AvaloniaProperty.Register<AdvancedWindow, bool>(nameof(ShowTitleProperty), true);
        
        public static readonly StyledProperty<IImage?> CustomIconProperty =
            AvaloniaProperty.Register<AdvancedWindow, IImage?>(nameof(CustomIcon));

        public static readonly StyledProperty<Control?> TitleBarContentProperty =
            AvaloniaProperty.Register<AdvancedWindow, Control?>(nameof(TitleBarContent));
        
        public static readonly StyledProperty<Control?> BottomContentProperty =
            AvaloniaProperty.Register<AdvancedWindow, Control?>(nameof(BottomContent));
        
        public static readonly StyledProperty<HorizontalAlignment> HorizontalAlignmentTitleProperty =
            AvaloniaProperty.Register<AdvancedWindow, HorizontalAlignment>(nameof(HorizontalAlignmentTitle), HorizontalAlignment.Left);

        protected override Type StyleKeyOverride => typeof(AdvancedWindow);

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

        public HorizontalAlignment HorizontalAlignmentTitle
        {
            get => GetValue(HorizontalAlignmentTitleProperty);
            set => SetValue(HorizontalAlignmentTitleProperty, value);
        }

        public AdvancedWindow()
        {
#if DEBUG
            this.AttachDevTools();
#endif
            
            if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                HorizontalAlignmentTitle = HorizontalAlignment.Center;
                Classes.Add("PlatformOSX");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Classes.Add("PlatformWindows");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Classes.Add("PlatformLinux");
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == WindowStateProperty)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    //ExtendClientAreaTitleBarHeightHint = WindowState is WindowState.Maximized or WindowState.FullScreen ? 37 : 30;
                }
            }
        }
        
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (Content is FlexibleWindow {DataContext: FlexibleWindowViewModelBase {IsDirty: true} vm} flexibleWindow)
            {
                e.Cancel = true;
                vm.Close(flexibleWindow);
            }
            base.OnClosing(e);
        }
    }
}