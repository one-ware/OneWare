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
        {
            foreach (var parameter in contract.CreatorParameters){
                if(parameter.PropertyType is not {} targetType || !_provider.IsRegistered(targetType))
                {
                    // If the object has a parameter that is not registered (like EditViewModel) we can use OverrideCreator
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
            }
            contract.DefaultCreator = () => _provider.Resolve(objectType);
        }

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

    /// <inheritdoc/>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        var property = base.CreateProperty(member, memberSerialization);

        // Never serialize the Owner back-reference. Owner is fully rebuilt by
        // Factory.InitLayout/InitDockable on load, so it is redundant on disk.
        // Worse, with PreserveReferencesHandling the first occurrence of an object
        // is written in full and later ones as $ref. Because a pinned dockable's
        // Owner chain points up into the shared structural tree (ToolDock ->
        // RightPane -> MainLayout -> root), the entire layout gets serialized nested
        // *inside* that pinned dockable. If the pinned dockable's type can no longer
        // be resolved (e.g. an uninstalled plugin), the whole token is skipped on
        // read and every $ref into the structure resolves to null, wiping the layout.
        //
        // Suppress Owner on WRITE only (do not set Ignored) so existing saved layouts
        // that still contain Owner are read correctly and can be salvaged.
        if (property.PropertyName == "Owner")
            property.ShouldSerialize = _ => false;

        return property;
    }
}