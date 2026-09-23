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

    // A breakpoint changed its state while the collection stayed the same. Needed because a
    // margin listening to CollectionChanged alone would never see a refusal by the target.
    public event EventHandler? VerificationChanged;

    public void Add(BreakPoint bp)
    {
        Breakpoints.Add(bp);
    }

    public void Remove(BreakPoint bp)
    {
        Breakpoints.Remove(bp);
    }

    // Report only a change that really happened: every editor currently open repaints its
    // margin on this.
    public void SetVerified(BreakPoint bp, bool verified)
    {
        if (bp.IsVerified == verified) return;

        bp.IsVerified = verified;
        VerificationChanged?.Invoke(this, EventArgs.Empty);
    }

    // Once a session has ended, no target says anything about the breakpoints any more. They
    // stay, but from here on a hollow dot would be a claim with nothing behind it.
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