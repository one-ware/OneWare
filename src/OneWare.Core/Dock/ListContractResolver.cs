using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dock.Model.Mvvm.Controls;
using DryIoc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneWare.Core.Views.DockViews;
using Prism.DryIoc;
using Prism.Ioc;

namespace OneWare.Core.Dock;

public class ListContractResolver : DefaultContractResolver
{
    private readonly Type _type;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="type"></param>
    public ListContractResolver(Type type)
    {
        _type = type;
    }

    /// <inheritdoc/>
    public override JsonContract ResolveContract(Type type)
    {
        if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
        {
            return base.ResolveContract(_type.MakeGenericType(type.GenericTypeArguments[0]));
        }
        
        var contract = base.ResolveContract(type);

        if (contract is JsonObjectContract co)
        {
            if (ContainerLocator.Container.GetContainer().IsRegistered(type))
            {
                co.OverrideCreator = (parameters) =>
                {
                    var resolveParameters = parameters
                        .Where(x => x != null)
                        .Select(x => (x?.GetType(), x))
                        .ToArray();
                    
                    var resolve = ContainerLocator.Container.Resolve(type, resolveParameters);
                    return resolve;
                };
            }
        }
        return contract;
    }

    /// <inheritdoc/>
    protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
    {
        return base.CreateProperties(type, memberSerialization).Where(p => p.Writable).ToList();
    }
}