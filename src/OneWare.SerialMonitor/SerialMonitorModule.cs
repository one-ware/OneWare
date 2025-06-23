// OneWare.SerialMonitor/SerialMonitorModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.Essentials.Services;
using OneWare.SerialMonitor.ViewModels;

namespace OneWare.SerialMonitor;

public class SerialMonitorModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Register types with Autofac
        builder.RegisterType<SerialMonitorViewModel>()
               .As<ISerialMonitorService>()
               .AsSelf()
               .SingleInstance();

        // Register the initializer for this module as a singleton
        builder.RegisterType<SerialMonitorModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }

    // The OnInitialized method will be removed from here.
    // Its logic will be moved to SerialMonitorModuleInitializer.
}