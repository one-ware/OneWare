using System.Collections;
using Newtonsoft.Json;
using OneWare.Essentials.ViewModels;

namespace OneWare.Core.Dock;

public sealed class NoSerializeLayoutListConverter : JsonConverter
{
    public override bool CanRead => false;

    public override bool CanConvert(Type objectType)
    {
        return typeof(IList).IsAssignableFrom(objectType)
               || objectType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        if (value is not IEnumerable enumerable)
        {
            serializer.Serialize(writer, value);
            return;
        }

        writer.WriteStartArray();
        foreach (var item in enumerable)
        {
            if (item is INoSerializeLayout) continue;
            serializer.Serialize(writer, item);
        }
        writer.WriteEndArray();
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException("NoSerializeLayoutListConverter does not support reading.");
    }
}
