// OneWare.SearchList/SearchListModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.SearchList.ViewModels;

namespace OneWare.SearchList;

public class SearchListModule : Module // Inherit from Autofac.Module
{
    // The constructor taking IDockService and IWindowService will be removed from here.
    // Module.Load is where registrations happen, not initialization.

    protected override void Load(ContainerBuilder builder)
    {
        // Register types with Autofac
        builder.RegisterType<SearchListViewModel>().AsSelf().SingleInstance(); // Register as self for direct injection

        // Register the initializer for this module as a singleton
        builder.RegisterType<SearchListModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }

    // The OnInitialized method will be removed from here.
    // Its logic will be moved to SearchListModuleInitializer.
}