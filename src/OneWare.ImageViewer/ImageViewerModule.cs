using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.ImageViewer.ViewModels;

namespace OneWare.ImageViewer;

public class ImageViewerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<ImageViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IMainDockService>().RegisterDocumentView<ImageViewModel>(".svg", ".jpg", ".png", ".jpeg");
    }
}

