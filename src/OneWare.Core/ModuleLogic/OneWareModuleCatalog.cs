using System.Reflection;
using OneWare.Essentials.Services;

namespace OneWare.Core.ModuleLogic;

public sealed class OneWareModuleCatalog
{
    private readonly List<IOneWareModule> _modules = new();

    public IReadOnlyList<IOneWareModule> Modules => _modules;

    public OneWareModuleCatalog AddModule<T>() where T : IOneWareModule, new()
    {
        return AddModule(new T());
    }

    public OneWareModuleCatalog AddModule(IOneWareModule module)
    {
        if (_modules.Any(x => string.Equals(x.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
            return this;

        _modules.Add(module);
        return this;
    }

    public IReadOnlyList<IOneWareModule> AddModulesFromAssembly(Assembly assembly)
    {
        var added = new List<IOneWareModule>();
        Type[] candidates;
        try
        {
            candidates = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            candidates = ex.Types.Where(x => x != null).Cast<Type>().ToArray();
        }

        var types = candidates
            .Where(x => !x.IsAbstract && typeof(IOneWareModule).IsAssignableFrom(x));

        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not IOneWareModule module)
                continue;

            if (_modules.Any(x => string.Equals(x.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            _modules.Add(module);
            added.Add(module);
        }

        return added;
    }
}