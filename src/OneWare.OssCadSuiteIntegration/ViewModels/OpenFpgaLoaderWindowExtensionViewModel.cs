using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Prism.Ioc;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class OpenFpgaLoaderWindowExtensionViewModel : ObservableObject
{
    private readonly IFpga? _fpga;
    private readonly UniversalFpgaProjectRoot _projectRoot;
    private readonly IWindowService _windowService;

    public OpenFpgaLoaderWindowExtensionViewModel(UniversalFpgaProjectRoot projectRoot, IWindowService windowService,
        FpgaService fpgaService)
    {
        _windowService = windowService;
        _projectRoot = projectRoot;

        _fpga = fpgaService.Fpgas.FirstOrDefault(x => x.Name == projectRoot.GetProjectProperty("Fpga"));

        IsVisible = projectRoot.Loader is OpenFpgaLoader;
        IsEnabled = _fpga != null;
    }

    public bool IsVisible { get; }

    public bool IsEnabled { get; }

    public async Task OpenSettingsAsync(Control control)
    {
        if (_fpga == null) return;

        var ownerWindow = TopLevel.GetTopLevel(control) as Window;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await _windowService.ShowDialogAsync(
                    new OpenFpgaLoaderSettingsView
                        { DataContext = new OpenFpgaLoaderSettingsViewModel(_projectRoot, _fpga) }, ownerWindow);
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }
        });
    }
}