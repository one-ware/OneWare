using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OneWare.PackageManager.ViewModels;
using VHDPlus.Shared;

namespace OneWare.PackageManager.Views
{
    public partial class PackageManagerWindow : AdvancedWindow
    {
        public static PackageManagerWindow LastInstance { get; private set; }
        
        public PackageManagerWindow()
        {
            LastInstance = this;
            
            DataContext = Global.PackageManagerViewModel;

            InitializeComponent();

#if DEBUG
            this.AttachDevTools();
#endif

            AddHandler(SearchBox.SearchEvent, (o, i) =>
            {
                if (DataContext is PackageManagerViewModel pvm) pvm.OnSearch();
            }, RoutingStrategies.Bubble);
        }
        


        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            LastInstance = this;
        }
    }
}