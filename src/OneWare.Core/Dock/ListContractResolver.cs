using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Dock;

public class ListContractResolver : DefaultContractResolver
{
    private readonly Type _type;

    /// <summary>
    /// </summary>
    /// <param name="type"></param>
    public ListContractResolver(Type type)
    {
        _type = type;
    }

    /// <inheritdoc />
    public override JsonContract ResolveContract(Type type)
    {
        if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            return base.ResolveContract(_type.MakeGenericType(type.GenericTypeArguments[0]));

        var contract = base.ResolveContract(type);

        if (contract is JsonObjectContract co)
        {
            if (ContainerLocator.Container?.IsRegistered(type) == true)
                co.OverrideCreator = parameters =>
                {
                    var resolveParameters = parameters
                        .Where(x => x != null)
                        .Select(x => (x!.GetType(), x))
                        .ToArray();

                    var resolve = ContainerLocator.Container!.Resolve(type, resolveParameters);
                    return resolve;
                };
        }
            
        return contract;
    }

    /// <inheritdoc />
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        var properties = base.CreateProperties(type, memberSerialization).Where(p => p.Writable).ToList();

        foreach (var property in properties)
        {
            var existingShouldSerialize = property.ShouldSerialize;
            property.ShouldSerialize = instance =>
            {
                if (existingShouldSerialize != null && !existingShouldSerialize(instance))
                    return false;

                var value = property.ValueProvider?.GetValue(instance);
                return value is not INoSerializeLayout;
            };
        }

        return properties;
    }
}
