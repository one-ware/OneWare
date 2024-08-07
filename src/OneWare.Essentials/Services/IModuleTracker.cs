namespace OneWare.Essentials.Services;

public interface IModuleTracker
{
    /// <summary>
    ///     Records the module has been loaded
    /// </summary>
    /// <param name="moduleName">The <see cref="ModuleNames">known name</see> of the module</param>
    void RecordModuleLoaded(string moduleName);

    /// <summary>
    ///     Record the module has been constructed
    /// </summary>
    /// <param name="moduleName">The <see cref="ModuleNames">known name</see> of the module</param>
    void RecordModuleConstructed(string moduleName);

    /// <summary>
    ///     Record the module has been initialized
    /// </summary>
    /// <param name="moduleName">The <see cref="ModuleNames">known name</see> of the module</param>
    void RecordModuleInitialized(string moduleName);
}