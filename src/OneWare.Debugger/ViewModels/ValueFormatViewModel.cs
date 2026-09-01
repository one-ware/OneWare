using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Debugger.Models;
using OneWare.Essentials.Services;

namespace OneWare.Debugger.ViewModels;

// Die Anzeigeeinstellung, die Memory, Registers und Variables gemeinsam benutzen.
// Ein einziges Objekt fuer alle drei -> die Leiste steht ueber jeder Tabelle, zeigt aber
// ueberall denselben Zustand, und ein Umschalten wirkt sofort in allen dreien.
// Gehalten wird der Wert ueber ISettingsService.Register: gespeichert und damit ueber
// Neustarts hinweg stabil, aber ohne eigenen Eintrag auf der Einstellungsseite -> bedient
// wird er im Panel, und dieselbe Sache an zwei Orten anzubieten stiftet nur Verwirrung.
public partial class ValueFormatViewModel : ObservableObject
{
    public const string BaseSetting = "FEntwumS_Debugger_ValueBase";
    public const string SignedSetting = "FEntwumS_Debugger_ValueSigned";

    private readonly ISettingsService _settingsService;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSignedEnabled))] [NotifyPropertyChangedFor(nameof(IsHex))]
    [NotifyPropertyChangedFor(nameof(IsDec))] [NotifyPropertyChangedFor(nameof(IsOct))]
    [NotifyPropertyChangedFor(nameof(IsBin))]
    private NumberBase _selectedBase;

    [ObservableProperty] private bool _isSigned;

    public ValueFormatViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;

        // HasSetting statt blind zu lesen: das Modul registriert die Voreinstellungen zwar beim
        // Start, aber dieses Ansichtsmodell haengt nicht davon ab, in welcher Reihenfolge das
        // geschieht.
        if (settingsService.HasSetting(BaseSetting) &&
            Enum.TryParse<NumberBase>(settingsService.GetSettingValue<string>(BaseSetting), out var stored))
            _selectedBase = stored;

        if (settingsService.HasSetting(SignedSetting))
            _isSigned = settingsService.GetSettingValue<bool>(SignedSetting);
    }

    // Meldet jede Aenderung an die drei Panels, die daraufhin ihre Zeilen neu beschriften.
    // Bewusst ohne erneutes Lesen vom Ziel: die Rohwerte des letzten Halts liegen vor, und
    // waehrend das Ziel laeuft gaebe es ohnehin nichts zu holen.
    public event EventHandler? Changed;

    // Ein Vorzeichen gibt es nur dezimal -> bei Hex, Oktal und Binaer steht der Schalter grau.
    public bool IsSignedEnabled => SelectedBase == NumberBase.Dec;

    // Je eine Eigenschaft pro Radioknopf. Der Setter reagiert nur auf true: beim Umschalten
    // meldet der abgewaehlte Knopf sein false, und das darf die neue Wahl nicht zuruecknehmen.
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

    partial void OnIsSignedChanged(bool value)
    {
        Store(SignedSetting, value);
    }

    private void Store(string key, object value)
    {
        if (_settingsService.HasSetting(key)) _settingsService.SetSettingValue(key, value);

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
