namespace OneWare.Python;

public enum PythonInterpreterSource
{
    None,
    WorkspaceOverride,
    WorkspaceEnvironment,
    GlobalDefault,
    Path
}

public enum PythonInterpreterStatus
{
    Resolved,
    Missing,
    InvalidExplicitSelection
}

public sealed record PythonInterpreterResolution(
    string Workspace,
    string? ExecutablePath,
    PythonInterpreterSource Source,
    PythonInterpreterStatus Status,
    string Message)
{
    public bool IsResolved => Status == PythonInterpreterStatus.Resolved;
}

public sealed class PythonInterpreterChangedEventArgs(PythonInterpreterResolution resolution) : EventArgs
{
    public string Workspace => Resolution.Workspace;
    public PythonInterpreterResolution Resolution { get; } = resolution;
}

public sealed record PythonInterpreterCandidate(string ExecutablePath, string Label)
{
    public override string ToString() => $"{Label} - {ExecutablePath}";
}
