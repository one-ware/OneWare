using OneWare.Essentials.Debugger;
using OneWare.Essentials.Debugger.Interfaces;

namespace OneWare.Debugger.Models;

// Ein Eintrag in der Startauswahl der Debug-Leiste: entweder eine Programmdatei oder ein
// Vorbereiter, der sein Ziel erst hochfaehrt und die Anforderung danach selbst liefert.
// Anzeige und Pfad getrennt, weil der volle Pfad in der schmalen Leiste nicht lesbar waere,
// der Request aber genau ihn braucht. Als Record, damit ein neu eingelesener Eintrag mit
// demselben Pfad gleich ist wie der bisherige und die Auswahl in der ComboBox stehen bleibt -
// das gilt fuer Vorbereiter mit, weil sie als Singleton aus dem Container kommen und damit
// ueber die Referenz gleich sind.
// Path: Vollstaendiger Pfad der Programmdatei. Leer bei einem Vorbereiter: welche Datei es wird, steht erst
// nach dem Vorbereiten fest.
// Display: Was in der Liste steht - der Pfad relativ zum Projekt, sonst der Dateiname; bei einem Vorbereiter
// dessen IDebugLaunchProvider.DisplayName.
// Provider: Gesetzt, wenn der Eintrag fuer einen Vorbereiter steht. Dann laeuft der Start ueber
// IDebuggerService.StartAsync(IDebugLaunchProvider, CancellationToken) statt ueber eine selbst gebaute
// Anforderung.
public sealed record ExecutableOption(string Path, string Display, IDebugLaunchProvider? Provider = null)
{
    public override string ToString()
    {
        return Display;
    }
}
