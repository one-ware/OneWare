using OneWare.Essentials.Enums;
using Prism.Modularity;
using Prism.Mvvm;

namespace OneWare.Core.ModuleLogic;

internal class ModuleTrackingState : BindableBase
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
            if (_moduleName != value) base.SetProperty(ref _moduleName, value);
        }
    }

    public ModuleInitializationStatus ModuleInitializationStatus
    {
        get => _moduleInitializationStatus;
        set
        {
            if (_moduleInitializationStatus != value) base.SetProperty(ref _moduleInitializationStatus, value);
        }
    }

    public DiscoveryMethod ExpectedDiscoveryMethod
    {
        get => _expectedDiscoveryMethod;
        set
        {
            if (_expectedDiscoveryMethod != value) base.SetProperty(ref _expectedDiscoveryMethod, value);
        }
    }

    public InitializationMode ExpectedInitializationMode
    {
        get => _expectedInitializationMode;
        set
        {
            if (_expectedInitializationMode != value) base.SetProperty(ref _expectedInitializationMode, value);
        }
    }

    public string ConfiguredDependencies
    {
        get => _configuredDependencies;
        set
        {
            if (_configuredDependencies != value) base.SetProperty(ref _configuredDependencies, value);
        }
    }
}