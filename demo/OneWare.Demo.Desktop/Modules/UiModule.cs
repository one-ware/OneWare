using Autofac;
using OneWare.Core.Services;
using OneWare.Core.Views.Windows;
using OneWare.Demo.Desktop.ViewModels;
using OneWare.Essentials.Services;

namespace OneWare.Demo.Desktop.Modules
{
    public class UiModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Register SplashWindowViewModel
            // .AsSelf() means it can be resolved directly as SplashWindowViewModel
            // .SingleInstance() means Autofac will create only one instance and reuse it throughout the app's lifetime.
            // This is suitable for a splash screen view model that typically has one instance.
            builder.RegisterType<SplashWindowViewModel>().AsSelf().SingleInstance();

            // Example of other UI-related registrations you might have:
            // builder.RegisterType<MainViewModel>().AsSelf().SingleInstance();
            // builder.RegisterType<MainWindow>().AsSelf().SingleInstance(); // If your main window is a singleton
            builder.RegisterType<ChangelogView>().AsSelf(); // Views are often transient unless specific singleton behavior is needed         
        }
    }
}
