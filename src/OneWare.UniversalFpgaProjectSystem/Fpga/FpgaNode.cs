namespace OneWare.UniversalFpgaProjectSystem.Fpga;

public class FpgaNode
{
    public string Name { get; }
    
    public string Direction { get; }

    public FpgaNode(string name, string direction)
    {
        Name = name;
        Direction = direction;
    }
}