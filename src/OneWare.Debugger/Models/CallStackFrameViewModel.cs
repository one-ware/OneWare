using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.Debugger.Models;

public sealed partial class CallStackFrameViewModel : ObservableObject
{
    [ObservableProperty]
    private int _level;

    [ObservableProperty]
    private string? _function;

    [ObservableProperty]
    private string? _fileName;

    [ObservableProperty]
    private string? _fullPath;

    [ObservableProperty]
    private int _line;

    [ObservableProperty]
    private string? _address;

    public string Header => string.IsNullOrWhiteSpace(Function) ? "<unknown>" : Function;
    public string Location => !string.IsNullOrWhiteSpace(FileName) && Line > 0 ? $"{FileName}:{Line}" : Address ?? string.Empty;
    public string ToolTip => !string.IsNullOrWhiteSpace(FullPath) ? $"{FullPath}:{Line}" : Location;

    public static CallStackFrameViewModel FromModel(DebugStackFrame frame)
    {
        return new CallStackFrameViewModel
        {
            Level = frame.Level,
            Function = frame.Function,
            FileName = frame.FileName,
            FullPath = frame.FullPath,
            Line = frame.Line,
            Address = frame.Address
        };
    }
}
