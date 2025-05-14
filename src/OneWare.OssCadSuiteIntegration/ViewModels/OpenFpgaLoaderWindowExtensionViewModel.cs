using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Loaders;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using Microsoft.Extensions.Logging;
using Autofac;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class OpenFpgaLoaderWindowExtensionViewModel : ObservableObject
{
    private readonly IFpga? _fpga;
    private readonly UniversalFpgaProjectRoot _projectRoot;
    private readonly IWindowService _windowService;
    private readonly ILogger<OpenFpgaLoaderWindowExtensionViewModel> _logger;

    // Constructor injection for dependencies
    public OpenFpgaLoaderWindowExtensionViewModel(
        UniversalFpgaProjectRoot projectRoot,
        IWindowService windowService,
        FpgaService fpgaService,
        ILogger<OpenFpgaLoaderWindowExtensionViewModel> logger)
    {
        _windowService = windowService;
        _projectRoot = projectRoot;
        _logger = logger;

        _fpga = fpgaService.FpgaPackages.FirstOrDefault(x => x.Name == projectRoot.GetProjectProperty("Fpga"))?.LoadFpga();

        IsVisible = projectRoot.Loader is OpenFpgaLoader;
        IsEnabled = _fpga != null;
    }

    public bool IsVisible { get; }

    public bool IsEnabled { get; }

    // Open settings async with error handling
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
                    {
                        DataContext = new OpenFpgaLoaderSettingsViewModel(_projectRoot, _fpga)
                    },
                    ownerWindow);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error opening settings dialog.");
            }
        });
    }
}
