using Microsoft.Extensions.DependencyInjection;
using OneWare.Essentials.Services;
using OneWare.MarkdownViewer.ViewModels;

namespace OneWare.MarkdownViewer;

public class MarkdownViewerModule : OneWareModuleBase
{
    public override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<MarkdownEditViewModel>();
    }

    public override void Initialize(IServiceProvider serviceProvider)
    {
        serviceProvider.Resolve<IMainDockService>()
            .RegisterDocumentView<MarkdownEditViewModel>(".md", ".markdown");
    }
}

