using Dock.Model.Mvvm.Controls;

namespace OneWare.Core.Dock;

/// <summary>
/// Placeholder dockable substituted for a dockable whose concrete type can no
/// longer be resolved during layout deserialization (for example a tool from an
/// uninstalled or renamed plugin).
///
/// Returning a real, constructible dockable instead of <c>null</c> lets
/// Newtonsoft read the object's nested content — most importantly its serialized
/// <c>Owner</c> chain. Under PreserveReferencesHandling the shared structural tree
/// is often first defined inside such a dockable, so skipping it (null) would drop
/// every $ref into the structure and wipe the layout. The placeholder keeps those
/// references intact; <see cref="Services.MainDockService"/> removes the
/// placeholders after the layout has been rebuilt.
/// </summary>
public sealed class MissingDockable : Tool;
