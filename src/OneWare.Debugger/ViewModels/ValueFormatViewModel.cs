using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.Models;
using OneWare.Essentials.Services;

namespace OneWare.Debugger.ViewModels;

// The display setting that Memory, Registers and Variables share.
// A single object for all three -> a single Base selector in the debugger toolbar,
// shown while one of the three tabs is open, switches all of them immediately.
// The value is held through ISettingsService.Register: stored and therefore stable across
// restarts, but without its own entry on the settings page -> it is operated
// in the panel, and offering the same thing in two places only causes confusion.
public partial class ValueFormatViewModel : ObservableObject
{
    public const string BaseSetting = "Debugger_ValueBase";

    private readonly ISettingsService _settingsService;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsHex))]
    [NotifyPropertyChangedFor(nameof(IsDec))] [NotifyPropertyChangedFor(nameof(IsOct))]
    [NotifyPropertyChangedFor(nameof(IsBin))]
    private NumberBase _selectedBase;

    [ObservableProperty] private bool _isLocked;

    public ValueFormatViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // HasSetting instead of reading blindly: the module does register the defaults at
        // startup, but this view model does not depend on the order in which that
        // happens.
        if (settingsService.HasSetting(BaseSetting) &&
            Enum.TryParse<NumberBase>(settingsService.GetSettingValue<string>(BaseSetting), out var stored))
            _selectedBase = stored;
    }

    public IReadOnlyList<NumberBase> Bases { get; } = Enum.GetValues<NumberBase>();

    // Reports every change to the three panels, which then relabel their rows.
    // Deliberately without reading the target again: the raw values of the last halt are at hand, and
    // while the target runs there would be nothing to fetch anyway.
    public event EventHandler? Changed;

    // One property per radio button. The setter only reacts to true: on switching,
    // the deselected button reports its false, and that must not undo the new choice.
    public bool IsHex
    {
        get => SelectedBase == NumberBase.Hex;
        set
        {
            if (value) SelectedBase = NumberBase.Hex;
        }
    }

    public bool IsDec
    {
        get => SelectedBase == NumberBase.Dec;
        set
        {
            if (value) SelectedBase = NumberBase.Dec;
        }
    }

    public bool IsOct
    {
        get => SelectedBase == NumberBase.Oct;
        set
        {
            if (value) SelectedBase = NumberBase.Oct;
        }
    }

    public bool IsBin
    {
        get => SelectedBase == NumberBase.Bin;
        set
        {
            if (value) SelectedBase = NumberBase.Bin;
        }
    }

    partial void OnSelectedBaseChanged(NumberBase value)
    {
        Store(BaseSetting, value.ToString());
    }

    private void Store(string key, object value)
    {
        if (_settingsService.HasSetting(key)) _settingsService.SetSettingValue(key, value);

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
