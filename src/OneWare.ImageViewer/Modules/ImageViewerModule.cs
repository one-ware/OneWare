
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using OneWare.Essentials.Adapters;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Interfaces;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;
using OneWare.ImageViewer.ViewModels;
using Prism.Ioc;

namespace OneWare.ImageViewer.Modules
{
    public class ImageViewerModule(IContainerAdapter containerAdapter) : IOneWareModule
    {
        private readonly IContainerAdapter _containerAdapter = containerAdapter;

        public void RegisterTypes()
        {
            _containerAdapter.Register<ImageViewModel, ImageViewModel>();

            OnExecute();
        }

        public void OnExecute()
        {
            _containerAdapter.Resolve<IDockService>().RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png", ".jpeg");
        }
    }
}