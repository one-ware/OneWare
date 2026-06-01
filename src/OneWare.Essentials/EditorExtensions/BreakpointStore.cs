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
    public ObservableCollection<BreakPoint> Breakpoints { get; } = new();

    public BreakPoint? CurrentBreakPoint
    {
        get => _currentBreakPoint;
        set => SetProperty(ref _currentBreakPoint, value);
    }

    public void Add(BreakPoint bp)
    {
        Breakpoints.Add(bp);
    }

    public void Remove(BreakPoint bp)
    {
        Breakpoints.Remove(bp);
    }
}