using Newtonsoft.Json.Serialization;

namespace OneWare.Core.Dock;

public class SilentErrorSerializationBinder : DefaultSerializationBinder
{
    public override Type BindToType(string? assemblyName, string typeName)
    {
        try
        {
            return base.BindToType(assemblyName, typeName);
        }
        catch
        {
            return null; 
        }
    }
}