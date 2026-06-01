namespace OneWare.Debugger;

public interface IDebugAdapter
{
    string Id { get; }
    string DisplayName { get; }
    string Description { get; }

    bool CanLaunch(DebugLaunchRequest launchRequest);
    IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}
