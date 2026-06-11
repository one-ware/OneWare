using System.Collections.ObjectModel;
using System.Text;
using Dock.Model.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OneWare.Core.Dock;

public sealed class OneWareDockSerializer : IDockSerializer
{
    private readonly JsonSerializerSettings _settings;
    
    public OneWareDockSerializer(IServiceProvider provider, ILogger logger)
    {
        _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Objects,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            ContractResolver = new OneWareContractResolver(typeof(ObservableCollection<>), provider),
            NullValueHandling = NullValueHandling.Ignore,
            SerializationBinder = new SilentErrorSerializationBinder(),
            Converters =
            {
                new NoSerializeLayoutListConverter(),
                new KeyValuePairConverter()
            },
            Error = (sender, args) => 
            {
                logger.LogError($"JSON Deserialization Error at {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true;
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
