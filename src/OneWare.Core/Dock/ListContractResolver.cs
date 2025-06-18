using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac; // Using Autofac's ILifetimeScope
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace OneWare.Core.Dock
{
    public class ListContractResolver : DefaultContractResolver
    {
        private readonly Type _concreteListType; // Renamed for clarity: this is the concrete list type (e.g., typeof(List<>))
        private readonly ILifetimeScope _lifetimeScope; // Changed from IContainer to ILifetimeScope

        /// <summary>
        /// Constructor accepts concrete list type and Autofac lifetime scope instance
        /// </summary>
        /// <param name="concreteListType">The concrete generic list type (e.g., typeof(List&lt;&gt;)) to use for IList&lt;&gt; resolution.</param>
        /// <param name="lifetimeScope">The Autofac lifetime scope used for resolving types during deserialization.</param>
        public ListContractResolver(Type concreteListType, ILifetimeScope lifetimeScope)
        {
            _concreteListType = concreteListType ?? throw new ArgumentNullException(nameof(concreteListType));
            _lifetimeScope = lifetimeScope ?? throw new ArgumentNullException(nameof(lifetimeScope));
        }

        /// <inheritdoc />
        public override JsonContract ResolveContract(Type type)
        {
            // Handle IList<T> by resolving to the concrete List<T>
            if (type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>))
            {
                // Ensure the concrete list type is itself a generic type definition
                if (!_concreteListType.IsGenericTypeDefinition)
                {
                    throw new InvalidOperationException($"The provided concreteListType ({_concreteListType.Name}) must be a generic type definition (e.g., typeof(List<>)).");
                }
                return base.ResolveContract(_concreteListType.MakeGenericType(type.GenericTypeArguments[0]));
            }

            var contract = base.ResolveContract(type);

            if (contract is JsonObjectContract co)
            {
                // Check if the type is registered in the current lifetime scope
                // Using TryResolve avoids exceptions for unregistered types
                if (_lifetimeScope.IsRegistered(type))
                {
                    co.OverrideCreator = parameters =>
                    {
                        // Note: If you need to pass specific constructor parameters from JSON,
                        // this approach will need to be enhanced (e.g., using a custom JsonConverter
                        // for the specific type, or a factory that can interpret 'parameters').
                        // For now, it will resolve dependencies as usual via Autofac's constructor injection.
                        try
                        {
                            // Resolve the type from the lifetime scope. Autofac will handle its dependencies.
                            var resolvedInstance = _lifetimeScope.Resolve(type);
                            return resolvedInstance;
                        }
                        catch (Exception ex)
                        {
                            // Log the error if a type cannot be resolved during deserialization
                            // You might want to inject a logger into this class for better logging
                            Console.WriteLine($"Error resolving type {type.FullName} during deserialization: {ex.Message}");
                            throw; // Re-throw to indicate a deserialization failure
                        }
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