using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Essentials.EditorExtensions;

public class BreakpointStore : ObservableObject
{
    private BreakPoint? _currentBreakPoint;
    public ObservableCollection<BreakPoint> Breakpoints { get; } = new();

    public BreakPoint? CurrentBreakPoint
    {
        get => _currentBreakPoint;
        set => SetProperty(ref _currentBreakPoint, value);
    }

    public void Add(BreakPoint bp)
    {
        //if (!MainDock.Debugger.IsDebugging || MainDock.Debugger.InsertBreakpoint(bp))
        Breakpoints.Add(bp);
    }

    public void Remove(BreakPoint bp)
    {
        //if (!MainDock.Debugger.IsDebugging || MainDock.Debugger.RemoveBreakpoint(bp))
        Breakpoints.Remove(bp);
    }
}