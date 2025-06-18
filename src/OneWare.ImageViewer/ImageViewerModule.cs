// OneWare.ImageViewer/ImageViewerModuleInitializer.cs
using OneWare.Essentials.Services;
using OneWare.ImageViewer.ViewModels;
using System;

namespace OneWare.ImageViewer
{
    public class ImageViewerModuleInitializer
    {
        private readonly IDockService _dockService;

        public ImageViewerModuleInitializer(IDockService dockService)
        {
            _dockService = dockService ?? throw new ArgumentNullException(nameof(dockService));
        }

        public void Initialize()
        {
            _dockService.RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png", ".jpeg");
        }
    }
}