using System.ComponentModel;
using System.Runtime.CompilerServices;
using OneWare.Essentials.Enums;
using OneWare.Core.Enums; // Assuming you create ModuleInitializationMode here

namespace OneWare.Core.ModuleLogic;

internal class ModuleTrackingState : INotifyPropertyChanged
{
    private string _configuredDependencies = "(none)";
    private DiscoveryMethod _expectedDiscoveryMethod;
    private ModuleInitializationMode _expectedInitializationMode;
    private ModuleInitializationStatus _moduleInitializationStatus;
    private string? _moduleName;

    public string? ModuleName
    {
        get => _moduleName;
        set => SetProperty(ref _moduleName, value);
    }

    public ModuleInitializationStatus ModuleInitializationStatus
    {
        get => _moduleInitializationStatus;
        set => SetProperty(ref _moduleInitializationStatus, value);
    }

    public DiscoveryMethod ExpectedDiscoveryMethod
    {
        get => _expectedDiscoveryMethod;
        set => SetProperty(ref _expectedDiscoveryMethod, value);
    }

    public ModuleInitializationMode ExpectedInitializationMode
    {
        get => _expectedInitializationMode;
        set => SetProperty(ref _expectedInitializationMode, value);
    }

    public string ConfiguredDependencies
    {
        get => _configuredDependencies;
        set => SetProperty(ref _configuredDependencies, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
