// OneWare.Output/OutputModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.Output.ViewModels;
using OneWare.Essentials.Services; // For IOutputService

namespace OneWare.Output;

public class OutputModule : Module // Inherit from Autofac.Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register OutputViewModel as a singleton, implementing IOutputService
        // Original: containerRegistry.RegisterManySingleton<OutputViewModel>(typeof(IOutputService), typeof(OutputViewModel));
        builder.RegisterType<OutputViewModel>()
               .AsSelf() // Register as OutputViewModel
               .As<IOutputService>() // Also register as IOutputService
               .SingleInstance();

        // Register the initializer for this module as a singleton
        builder.RegisterType<OutputModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }
}