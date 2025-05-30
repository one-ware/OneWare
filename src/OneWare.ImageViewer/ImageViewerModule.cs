﻿using OneWare.Essentials.Services;
using OneWare.ImageViewer.ViewModels;
using Prism.Ioc;
using Prism.Modularity;

namespace OneWare.ImageViewer;

public class ImageViewerModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ImageViewModel>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        containerProvider.Resolve<IDockService>().RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png", ".jpeg");
    }
}