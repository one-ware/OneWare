// OneWare.SourceControl/SourceControlModule.cs
using Autofac; // Essential for Autofac.Module
using OneWare.SourceControl.Settings;
using OneWare.SourceControl.ViewModels;

namespace OneWare.SourceControl;

public class SourceControlModule : Module
{
    public const string GitHubAccountNameKey = "SourceControl_GitHub_AccountName";

    protected override void Load(ContainerBuilder builder)
    {
        // Register types with Autofac
        builder.RegisterType<CompareFileViewModel>();
        builder.RegisterType<SourceControlViewModel>().AsSelf().SingleInstance(); // Register as self for direct injection
        builder.RegisterType<GitHubAccountSettingViewModel>().AsSelf().SingleInstance();

        // Register the initializer for this module as a singleton
        builder.RegisterType<SourceControlModuleInitializer>().AsSelf().SingleInstance();

        base.Load(builder);
    }

    // The OnInitialized method will be removed from here.
    // Its logic will be moved to SourceControlModuleInitializer.
}