using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData.Binding;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;
using OneWare.OssCadSuiteIntegration.Views;
using OneWare.OssCadSuiteIntegration.Yosys;
using OneWare.UniversalFpgaProjectSystem.Models;
using OneWare.UniversalFpgaProjectSystem.Services;
using OneWare.UniversalFpgaProjectSystem.ViewModels;

namespace OneWare.OssCadSuiteIntegration.ViewModels;

public class YosysCompileWindowExtensionViewModel : ObservableObject
{
    private readonly FpgaService _fpgaService;
    private readonly UniversalFpgaProjectPinPlannerViewModel _pinPlannerViewModel;
    private readonly IProjectExplorerService _projectExplorerService;
    private readonly IWindowService _windowService;

    private bool _isVisible;

    public YosysCompileWindowExtensionViewModel(UniversalFpgaProjectPinPlannerViewModel pinPlannerViewModel,
        IWindowService windowService, IProjectExplorerService projectExplorerService, FpgaService fpgaService)
    {
        _pinPlannerViewModel = pinPlannerViewModel;
        _windowService = windowService;
        _projectExplorerService = projectExplorerService;
        _fpgaService = fpgaService;

        IDisposable? disposable = null;
        projectExplorerService.WhenValueChanged(x => x.ActiveProject).Subscribe(x =>
        {
            if (x is UniversalFpgaProjectRoot fpgaProjectRoot)
            {
                disposable?.Dispose();
                disposable = fpgaProjectRoot.WhenValueChanged(y => y.Toolchain).Subscribe(z =>
                {
                    IsVisible = z is YosysToolchain;
                });
            }
        });
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public async Task OpenCompileSettingsAsync(Control control)
    {
        var ownerWindow = TopLevel.GetTopLevel(control) as Window;
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                if (_projectExplorerService.ActiveProject is UniversalFpgaProjectRoot fpgaProjectRoot)
                {
                    var selectedFpga = _pinPlannerViewModel.SelectedFpgaModel?.Fpga;

                    if (selectedFpga == null) return;

                    await _windowService.ShowDialogAsync(
                        new YosysCompileSettingsView
                            { DataContext = new YosysCompileSettingsViewModel(fpgaProjectRoot, selectedFpga) },
                        ownerWindow);
                }
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            }
        });
    }
}