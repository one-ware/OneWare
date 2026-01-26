using Avalonia.Media;
using OneWare.CloudIntegration.ViewModels;
using OneWare.Essentials.Models;

namespace OneWare.CloudIntegration.Settings;

public class OneWareCloudAccountSetting : CustomSetting
{
    private object _value;

    public OneWareCloudAccountSetting() : base(string.Empty)
    {
        Control = new OneWareCloudAccountSettingViewModel(this);
        _value = string.Empty;
    }

    // Will be the User ID
    public override object Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value)) IsLoggedIn = !string.IsNullOrWhiteSpace(value.ToString());
        }
    }

    public IImage? Image
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string? Email
    {
        get => field ?? (IsLoggedIn ? "..." : "Not logged in");
        set => SetProperty(ref field, value);
    }

    public bool IsLoggedIn
    {
        get;
        set => SetProperty(ref field, value);
    }
}