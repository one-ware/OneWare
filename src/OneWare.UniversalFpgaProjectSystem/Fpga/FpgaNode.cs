namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaNode
{
    public FpgaNode(string name, string direction)
    {
        Name = name;
        Direction = direction;
    }

    public string Name { get; }

    public string Direction { get; }
}