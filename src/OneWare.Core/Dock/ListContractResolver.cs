using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OneWare.Core.Dock;

public class ListContractResolver : DefaultContractResolver
{
    private readonly Type _listImplementationType;
    private readonly Func<Type, (Type? Type, object?)[], object>? _resolveCallback;

    /// <summary>
    /// </summary>
    /// <param name="listImplementationType">Der konkrete Typ zur Auflösung von IList&lt;T&gt;</param>
    /// <param name="resolveCallback">Optional: Resolver-Funktion für Typen mit Parametern</param>
    public ListContractResolver(Type listImplementationType, Func<Type, (Type?, object?)[], object>? resolveCallback = null)
    {
        _listImplementationType = listImplementationType;
        _resolveCallback = resolveCallback;
    }

    public override JsonContract ResolveContract(Type type)
    {
        if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        {
            return base.ResolveContract(_listImplementationType.MakeGenericType(type.GenericTypeArguments[0]));
        }

        var contract = base.ResolveContract(type);

        if (_resolveCallback != null && contract is JsonObjectContract objectContract)
        {
            objectContract.OverrideCreator = parameters =>
            {
                var args = parameters
                    .Where(x => x != null)
                    .Select(x => (x?.GetType(), x))
                    .ToArray();

                return _resolveCallback(type, args);
            };
        }

        return contract;
    }

    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        return base.CreateProperties(type, memberSerialization).Where(p => p.Writable).ToList();
    }
}
