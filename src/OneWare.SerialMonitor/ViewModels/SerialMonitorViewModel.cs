using OneWare.Shared.Services;

namespace OneWare.SerialMonitor.ViewModels;

public class SerialMonitorViewModel : SerialMonitorBaseViewModel, ISerialMonitorService
{
    public const string IconKey = "BoxIcons.RegularUsb";
    public SerialMonitorViewModel(ISettingsService settingsService) : base(settingsService, IconKey)
    {
        Id = "SerialMonitor";
        Title = "Serial Monitor";
    }
}