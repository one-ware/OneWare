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
            // The type could not be resolved (e.g. a dockable from an uninstalled
            // or renamed plugin). Return a placeholder dockable instead of null so
            // Newtonsoft still reads the object's nested content — including the
            // serialized Owner chain that, under PreserveReferencesHandling, may
            // hold the first (full) definition of the shared structural tree.
            // Skipping the object (null) would drop those nested $id definitions and
            // null out every $ref into the layout. MainDockService removes the
            // placeholders after the layout is rebuilt.
            return typeof(MissingDockable);
        }
    }
}