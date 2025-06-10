using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneWare.Core.Adapters;

namespace OneWare.Core.Dock
{
    public class ListContractResolver : DefaultContractResolver
    {
        private readonly Type _type;
        private readonly IContainerAdapter _container;

        /// <summary>
        /// Constructor accepts type and container adapter instance
        /// </summary>
        /// <param name="type">Type to use for IList<> resolution</param>
        /// <param name="container">Container adapter used for resolving types</param>
        public ListContractResolver(Type type, IContainerAdapter container)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        /// <inheritdoc />
        public override JsonContract ResolveContract(Type type)
        {
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            {
                // Handle IList<T> by resolving to the concrete List<T>
                return base.ResolveContract(_type.MakeGenericType(type.GenericTypeArguments[0]));
            }

            var contract = base.ResolveContract(type);

            if (contract is JsonObjectContract co)
            {
                if (_container.IsRegistered(type))
                {
                    co.OverrideCreator = parameters =>
                    {
                        // Currently ignoring parameters because IContainerAdapter.Resolve signature does not support them
                        // You can extend the interface if you need to pass constructor parameters
                        var resolvedInstance = _container.Resolve(type);
                        return resolvedInstance;
                    };
                }
            }

            return contract;
        }

        /// <inheritdoc />
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            // Only serialize properties that have setters (are writable)
            return base.CreateProperties(type, memberSerialization).Where(p => p.Writable).ToList();
        }
    }
}
