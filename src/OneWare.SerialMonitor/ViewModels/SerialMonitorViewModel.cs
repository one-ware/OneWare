using OneWare.Shared.Services;

namespace OneWare.SerialMonitor.ViewModels;

public class SerialMonitorViewModel : SerialMonitorBaseViewModel, ISerialMonitorService
{
    public SerialMonitorViewModel(ISettingsService settingsService) : base(settingsService)
    {
        Id = "SerialMonitor";
        Title = "Serial Monitor";
    }
}