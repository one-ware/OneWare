// OneWare.TerminalManager/TerminalManagerModule.cs
using Autofac;
using OneWare.TerminalManager.ViewModels;

namespace OneWare.TerminalManager
{
    public class TerminalManagerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register types with Autofac
            builder.RegisterType<TerminalManagerViewModel>().AsSelf().SingleInstance();

            // Register the initializer for this module as a singleton
            builder.RegisterType<TerminalManagerModuleInitializer>().AsSelf().SingleInstance();

            base.Load(builder);
        }
        // OnInitialized method is removed from here
    }
}