using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dock.Model.Core;

namespace OneWare.Core.Dock;

public class DockableJsonConverter : JsonConverter<IDockable>
{
    public override IDockable? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, IDockable value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}