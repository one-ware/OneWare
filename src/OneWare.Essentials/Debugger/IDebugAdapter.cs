namespace OneWare.Essentials.Debugger;

public interface IDebugAdapter
{
    public string Id { get; }
    
    public string DisplayName { get; }
    
    public bool CanLaunch(DebugLaunchRequest launchRequest);
    
    public IDebugSession CreateSession(DebugLaunchRequest launchRequest);
}
