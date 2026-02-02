using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Dock;

public class OneWareContractResolver : DefaultContractResolver
{
    private readonly IServiceProvider _provider;
    private readonly Type _listType;

    public OneWareContractResolver(Type listType, IServiceProvider provider)
    {
        _provider = provider;
        _listType = listType;
    }

    protected override JsonObjectContract CreateObjectContract(Type objectType)
    {
        var contract = base.CreateObjectContract(objectType);
        
        if (_provider.IsRegistered(objectType))
            contract.OverrideCreator = parameters =>
            {
                var resolveParameters = parameters
                    .Where(x => x != null)
                    .Select(x => (x!.GetType(), x))
                    .ToArray();

                var resolve = _provider.Resolve(objectType, resolveParameters);
                return resolve;
            };

        return contract;
    }

    /// <inheritdoc />
    public override JsonContract ResolveContract(Type type)
    {
        if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        {
            return base.ResolveContract(_listType.MakeGenericType(type.GenericTypeArguments[0]));
        }
        return base.ResolveContract(type);
    }

    /// <inheritdoc/>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        return base.CreateProperties(type, memberSerialization)
            .Where(p =>
                p.AttributeProvider != null &&
                (p.AttributeProvider.GetAttributes(typeof(DataMemberAttribute), true).Any())
            )
            .ToList();
    }
}