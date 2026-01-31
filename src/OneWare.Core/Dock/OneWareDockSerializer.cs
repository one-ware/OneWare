using System.Collections.ObjectModel;
using System.Text;
using Dock.Model.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace OneWare.Core.Dock;

public sealed class OneWareDockSerializer : IDockSerializer
{
    private readonly JsonSerializerSettings _settings;
    
    public OneWareDockSerializer(IServiceProvider provider)
    {
        _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Objects,
            PreserveReferencesHandling = PreserveReferencesHandling.Objects,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            ContractResolver = new OneWareContractResolver(typeof(ObservableCollection<>), provider),
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new NoSerializeLayoutListConverter(),
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
