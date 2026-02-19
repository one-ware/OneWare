using OneWare.Essentials.ViewModels;

namespace OneWare.Debugger.ViewModels;

public class DebuggerViewModel : ExtendedTool
{
    public const string IconKey = "VsCodeLight.Debug";

    public DebuggerViewModel() : base(IconKey)
    {
        Id = "Debug";
    }

    public override void InitializeContent()
    {
        base.InitializeContent();
        Title = "Debug";
    }
}