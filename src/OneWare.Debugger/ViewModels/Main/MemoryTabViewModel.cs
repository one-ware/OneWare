using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneWare.Debugger.Helpers;
using OneWare.Debugger.Models;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.ViewModels.Main;

// Reiter "Memory": die vom Benutzer beobachteten Speicheradressen.
// Den Dienst braucht dieser Reiter nicht fuer Ereignisse, sondern nur zum Lesen - wann
// gelesen wird, sagt DebuggerViewModel.
public partial class MemoryTabViewModel : ObservableObject
{
    private readonly IDebuggerService _debuggerService;

    // Adresse, die der Eingabezeile entnommen und beim Hinzufuegen zur Beobachtungsliste
    // gemacht wird.
    [ObservableProperty] private string _addressText = string.Empty;

    [ObservableProperty] private MemoryRow? _selectedRow;

    public MemoryTabViewModel(IDebuggerService debuggerService, ValueFormatViewModel valueFormat)
    {
        _debuggerService = debuggerService;
        ValueFormat = valueFormat;

        // Das einzige Abo dieses Reiters, und es geht nicht ums Lesen: das Wasserzeichen gehoert
        // zum Ziel und steht erst mit der Sitzung fest. Wann gelesen wird, sagt weiterhin
        // DebuggerViewModel.
        _debuggerService.StateChanged += (_, _) => OnPropertyChanged(nameof(AddressWatermark));

        // Ein anderes Zahlensystem beschriftet nur neu, was schon gelesen ist.
        valueFormat.Changed += (_, _) => RenderAll();
    }

    // Zahlensystem und Vorzeichen der Anzeige. Dasselbe Objekt bedienen auch Registers und
    // Variables -> die Leiste ueber der Tabelle schaltet alle drei zugleich um.
    public ValueFormatViewModel ValueFormat { get; }

    // Beispieladresse im leeren Eingabefeld. Kommt vom Ziel -> im Kern steht keine Adresse einer
    // bestimmten Maschine mehr.
    public string AddressWatermark => _debuggerService.MemoryProfile.AddressWatermark;

    // Bleiben ueber Sessions hinweg stehen, damit man dieselben Adressen nach einem Neustart
    // nicht wieder eintippen muss.
    public ObservableCollection<MemoryRow> Watches { get; } = [];

    // Liest die ganze Beobachtungsliste neu. Nacheinander: das Backend beantwortet ohnehin nur
    // ein Kommando zur Zeit, und in der Reihenfolge der Liste zu lesen haelt die Anzeige
    // nachvollziehbar.
    public void Refresh()
    {
        _ = RefreshAllAsync();
    }

    // Nach dem Ende einer Sitzung stehen die Adressen weiter da, ihre Werte sind aber nichts
    // mehr wert.
    public void ClearValues()
    {
        foreach (var row in Watches)
        {
            row.Raw = string.Empty;
            row.Value = string.Empty;
        }
    }

    // Nimmt die eingetippte Adresse in die Beobachtungsliste auf und liest sie sofort, sofern
    // das Ziel gerade haelt. Ohne das sofortige Lesen stuende die neue Zeile bis zum naechsten
    // Halt leer da, und man wuesste nicht, ob die Adresse ueberhaupt lesbar ist.
    [RelayCommand]
    private async Task AddWatchAsync()
    {
        if (string.IsNullOrWhiteSpace(AddressText)) return;

        var row = new MemoryRow
        {
            Address = AddressText.Trim(),
            Length = _debuggerService.MemoryProfile.DefaultLength
        };
        Watches.Add(row);
        AddressText = string.Empty;

        row.PropertyChanged += OnRowChanged;

        await RefreshRowAsync(row);
    }

    [RelayCommand(CanExecute = nameof(CanRemoveWatch))]
    private void RemoveWatch()
    {
        if (SelectedRow is not { } row) return;

        row.PropertyChanged -= OnRowChanged;
        Watches.Remove(row);
        SelectedRow = null;
    }

    private bool CanRemoveWatch()
    {
        return SelectedRow is not null;
    }

    // Leert die ganze Beobachtungsliste statt nur der Auswahl. Auf einer leeren Liste ein
    // no-op, deshalb ohne eigenes CanExecute.
    [RelayCommand]
    private void ClearWatches()
    {
        foreach (var row in Watches) row.PropertyChanged -= OnRowChanged;
        Watches.Clear();
        SelectedRow = null;
    }

    partial void OnSelectedRowChanged(MemoryRow? value)
    {
        RemoveWatchCommand.NotifyCanExecuteChanged();
    }

    // Eine im Raster bearbeitete Adresse oder Laenge wird sofort neu gelesen. Der Wert selbst
    // ist ausgenommen, sonst loeste das Schreiben des Ergebnisses das naechste Lesen aus.
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MemoryRow row) return;
        if (e.PropertyName is not (nameof(MemoryRow.Address) or nameof(MemoryRow.Length))) return;

        _ = RefreshRowAsync(row);
    }

    private async Task RefreshRowAsync(MemoryRow row)
    {
        if (!_debuggerService.IsActive)
        {
            Show(row, string.Empty);
            return;
        }

        if (_debuggerService.State.IsRunning)
        {
            Show(row, "target running");
            return;
        }

        var profile = _debuggerService.MemoryProfile;
        var unitBytes = profile.AddressableUnitBytes;

        var value = await _debuggerService.ReadMemoryAsync(
            ToBackendAddress(row.Address, unitBytes),
            row.Length * unitBytes);

        Show(row, value == null ? "unreadable" : GroupIntoUnits(value, unitBytes, profile.IsLittleEndian));
    }

    // Haelt Rohwert und Anzeige beisammen. Ein Hinweis wie "unreadable" geht unveraendert durch
    // den Formatierer und steht danach genauso da wie zuvor.
    private void Show(MemoryRow row, string raw)
    {
        row.Raw = raw;
        row.Value = ValueFormatter.FormatHexUnits(raw, ValueFormat.SelectedBase, ValueFormat.IsSigned);
    }

    private void RenderAll()
    {
        foreach (var row in Watches)
            row.Value = ValueFormatter.FormatHexUnits(row.Raw, ValueFormat.SelectedBase, ValueFormat.IsSigned);
    }

    // Rechnet die eingetippte
    // Adresse von Zieleinheiten in Bytes um, indem das Backend sie rechnen
    // laesst -> im Kern steht kein Adressparser, und ein Symbol oder ein &-Ausdruck bleibt gueltig.
    // Der Cast ist noetig, weil sich ein Zeiger nicht multiplizieren laesst: "(x)*2" lehnt GDB mit
    // "Argument to arithmetic operation not a number" ab, "((long)(x))*2" nicht.
    private static string ToBackendAddress(string address, int unitBytes)
    {
        return unitBytes <= 1 ? address : $"((long)({address}))*{unitBytes}";
    }

    // Fasst die einzelnen Bytes zu Einheiten des Ziels zusammen. Bei Little Endian kommt das
    // niederwertige Byte zuerst -> zum Anzeigen umdrehen, damit die Zahl so dasteht, wie man sie
    // schreibt. Welche Reihenfolge gilt, sagt das Ziel ueber das Profil; frueher stand hier
    // Little Endian fest, und ein Big-Endian-Ziel haette jedes Wort byteverdreht angezeigt.
    private static string GroupIntoUnits(string spacedBytes, int unitBytes, bool littleEndian)
    {
        if (unitBytes <= 1) return spacedBytes;

        var bytes = spacedBytes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var units = new List<string>();

        for (var i = 0; i < bytes.Length; i += unitBytes)
        {
            var unit = bytes.Skip(i).Take(Math.Min(unitBytes, bytes.Length - i));

            units.Add(string.Concat(littleEndian ? unit.Reverse() : unit));
        }

        return string.Join(' ', units);
    }

    private async Task RefreshAllAsync()
    {
        foreach (var row in Watches.ToArray()) await RefreshRowAsync(row);
    }
}
