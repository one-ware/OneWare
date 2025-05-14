using OneWare.Essentials.Services;
using OneWare.ImageViewer.ViewModels;
using Autofac;

namespace OneWare.ImageViewer;

public class ImageViewerModule
{
    public void RegisterTypes(ContainerBuilder builder)
    {
        // Register ImageViewModel with Autofac
        builder.RegisterType<ImageViewModel>().AsSelf();
    }

    public void OnInitialized(ILifetimeScope containerScope)
    {
        // Resolve IDockService and register the ImageViewModel for specific file types
        var dockService = containerScope.Resolve<IDockService>();
        dockService.RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png");
    }
}
