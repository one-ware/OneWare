namespace OneWare.Debugger.Models;

/// <summary>
///     Ein Eintrag in der Executable-Auswahl der Debug-Leiste.
/// </summary>
/// <remarks>
///     Anzeige und Pfad getrennt, weil der volle Pfad in der schmalen Leiste nicht lesbar waere,
///     der Request aber genau ihn braucht. Als Record, damit ein neu eingelesener Eintrag mit
///     demselben Pfad gleich ist wie der bisherige und die Auswahl in der ComboBox stehen bleibt.
/// </remarks>
/// <param name="Path">Vollstaendiger Pfad der Programmdatei.</param>
/// <param name="Display">Was in der Liste steht - der Pfad relativ zum Projekt, sonst der
/// Dateiname.</param>
public sealed record ExecutableOption(string Path, string Display)
{
    public override string ToString()
    {
        return Display;
    }
}
