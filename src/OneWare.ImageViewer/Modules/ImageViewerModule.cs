
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ImageViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.ImageViewer.Modules
{
    public class ImageViewerModule
    {
        private readonly IContainerAdapter _containerAdapter;

        public ImageViewerModule(IContainerAdapter containerAdapter)
        {
            _containerAdapter = containerAdapter;
        }

        public void Load()
        {
            _containerAdapter.Register<ImageViewModel, ImageViewModel>();

            Register();
        }

        private void Register()
        {
            _containerAdapter.Resolve<IDockService>().RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png", ".jpeg");
        }
    }
}