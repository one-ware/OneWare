using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Essentials.EditorExtensions;

public class BreakpointStore : ObservableObject
{
    /// <summary>
    /// Shared, application-wide breakpoint store. All editors and debug sessions
    /// observe this same instance so that breakpoints set in any open file are
    /// available to the active debugger and survive editor close/re-open.
    /// </summary>
    public static BreakpointStore Instance { get; } = new();

    private BreakPoint? _currentBreakPoint;
    private bool _isTargetRunning;
    public ObservableCollection<BreakPoint> Breakpoints { get; } = new();

    public BreakPoint? CurrentBreakPoint
    {
        get => _currentBreakPoint;
        set => SetProperty(ref _currentBreakPoint, value);
    }

    public bool IsTargetRunning
    {
        get => _isTargetRunning;
        set => SetProperty(ref _isTargetRunning, value);
    }

    // Ein Breakpoint hat seinen Zustand gewechselt, ohne dass sich die Sammlung geaendert hat.
    // Braucht es, weil die Randspalte sonst nur an CollectionChanged haengt und eine Ablehnung
    // durch das Ziel damit nie zu sehen waere.
    public event EventHandler? VerificationChanged;

    public void Add(BreakPoint bp)
    {
        Breakpoints.Add(bp);
    }

    public void Remove(BreakPoint bp)
    {
        Breakpoints.Remove(bp);
    }

    // Nur melden, wenn sich wirklich etwas geaendert hat -> das Neuzeichnen der Randspalte
    // haengt an jedem Editor, der gerade offen ist.
    public void SetVerified(BreakPoint bp, bool verified)
    {
        if (bp.IsVerified == verified) return;

        bp.IsVerified = verified;
        VerificationChanged?.Invoke(this, EventArgs.Empty);
    }

    // Nach dem Ende einer Sitzung sagt kein Ziel mehr etwas ueber die Breakpoints aus. Sie
    // bleiben stehen, aber ein hohler Punkt waere ab hier eine Behauptung ohne Grundlage.
    public void ResetVerification()
    {
        var changed = false;

        foreach (var bp in Breakpoints)
        {
            if (bp.IsVerified) continue;

            bp.IsVerified = true;
            changed = true;
        }

        if (changed) VerificationChanged?.Invoke(this, EventArgs.Empty);
    }
}