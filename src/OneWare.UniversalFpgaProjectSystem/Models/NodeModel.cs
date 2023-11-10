namespace OneWare.UniversalFpgaProjectSystem.Models;

public class NodeModel
{
    public string Name { get; }

    public NodeModel(string name)
    {
        Name = name;
    }
    
    public override string ToString()
    {
        return Name;
    }
}