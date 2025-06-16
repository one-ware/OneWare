using OneWare.Essentials.Enums;
using Prism.Modularity;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Core.ModuleLogic;

internal partial class ModuleTrackingState : ObservableObject
{
    private string _configuredDependencies = "(none)";
    private DiscoveryMethod _expectedDiscoveryMethod;
    private InitializationMode _expectedInitializationMode;
    private ModuleInitializationStatus _moduleInitializationStatus;
    private string? _moduleName;

    public string? ModuleName
    {
        get => _moduleName;
        set
        {
            SetProperty(ref _moduleName, value);
        }
    }

    public ModuleInitializationStatus ModuleInitializationStatus
    {
        get => _moduleInitializationStatus;
        set
        {
            SetProperty(ref _moduleInitializationStatus, value);
        }
    }

    public DiscoveryMethod ExpectedDiscoveryMethod
    {
        get => _expectedDiscoveryMethod;
        set
        {
            SetProperty(ref _expectedDiscoveryMethod, value);
        }
    }

    public InitializationMode ExpectedInitializationMode
    {
        get => _expectedInitializationMode;
        set
        {
            SetProperty(ref _expectedInitializationMode, value);
        }
    }

    public string ConfiguredDependencies
    {
        get => _configuredDependencies;
        set
        {
            SetProperty(ref _configuredDependencies, value);
        }
    }
}