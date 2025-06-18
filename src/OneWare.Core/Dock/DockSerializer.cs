// OneWare.Core.Dock/DockSerializer.cs
using System;
using System.IO;
using System.Text;
using Dock.Model.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OneWare.Essentials.Converters; // Ensure this points to your modified ListContractResolver
using OneWare.Essentials.Services; // If ILogger or other services are needed here
using Autofac; // Add this for ILifetimeScope

namespace OneWare.Core.Dock;

/// <summary>
/// A class that implements the <see cref="IDockSerializer" /> interface using JSON serialization.
/// </summary>
public sealed class DockSerializer : IDockSerializer
{
    private readonly JsonSerializerSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="DockSerializer"/> class.
    /// </summary>
    /// <param name="listType">The generic list type to support for Dock serialization.</param>
    /// <param name="lifetimeScope">The Autofac lifetime scope for resolving dependencies during deserialization.</param>
    public DockSerializer(Type listType, ILifetimeScope lifetimeScope) // Use ILifetimeScope
    {
        // No longer using IContainerAdapter, but Autofac's ILifetimeScope
        _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Objects,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            // Pass the ILifetimeScope directly to ListContractResolver
            ContractResolver = new ListContractResolver(listType, lifetimeScope),
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new KeyValuePairConverter()
            }
        };
    }

    /// <inheritdoc />
    public string Serialize<T>(T value)
    {
        return JsonConvert.SerializeObject(value, _settings);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string text)
    {
        return JsonConvert.DeserializeObject<T>(text, _settings);
    }

    /// <inheritdoc />
    public T? Load<T>(Stream stream)
    {
        using var streamReader = new StreamReader(stream, Encoding.UTF8);
        var text = streamReader.ReadToEnd();
        return Deserialize<T>(text);
    }

    /// <inheritdoc />
    public void Save<T>(Stream stream, T value)
    {
        var text = Serialize(value);
        if (string.IsNullOrWhiteSpace(text)) return;
        using var streamWriter = new StreamWriter(stream, Encoding.UTF8);
        streamWriter.Write(text);
    }
}