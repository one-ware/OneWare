namespace OneWare.Vcd.Viewer.Context;

public class VcdContext
{
    public IEnumerable<char> OpenIds { get; }

    public VcdContext(IEnumerable<char> openIds)
    {
        OpenIds = openIds;
    }
}