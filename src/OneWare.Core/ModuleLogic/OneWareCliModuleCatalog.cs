using System.Reflection;
using OneWare.Essentials.Services;

namespace OneWare.Core.ModuleLogic;

public sealed class OneWareCliModuleCatalog
{
    private readonly List<IOneWareCliModule> _modules = new();

    public IReadOnlyList<IOneWareCliModule> Modules => _modules;

    public OneWareCliModuleCatalog AddModule<T>() where T : IOneWareCliModule, new()
    {
        return AddModule(new T());
    }

    public OneWareCliModuleCatalog AddModule(IOneWareCliModule module)
    {
        if (_modules.Any(x => string.Equals(x.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
            return this;

        _modules.Add(module);
        return this;
    }

    public IReadOnlyList<IOneWareCliModule> AddModulesFromAssembly(Assembly assembly)
    {
        var added = new List<IOneWareCliModule>();
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
            .Where(x => x.IsPublic &&
                        !x.IsAbstract &&
                        x.GetConstructor(Type.EmptyTypes) is not null &&
                        typeof(IOneWareCliModule).IsAssignableFrom(x));

        foreach (var type in types)
        {
            if (Activator.CreateInstance(type) is not IOneWareCliModule module)
                continue;

            if (_modules.Any(x => string.Equals(x.Id, module.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            _modules.Add(module);
            added.Add(module);
        }

        return added;
    }
}