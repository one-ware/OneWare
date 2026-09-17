using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace OneWare.Python;

internal static class PythonLanguageServerConfiguration
{
    public static JObject Create(string? interpreter)
    {
        var settings = new JObject
        {
            ["pyrefly"] = new JObject { ["diagnosticMode"] = "openFilesOnly" }
        };
        if (!string.IsNullOrWhiteSpace(interpreter)) settings["pythonPath"] = interpreter;
        return settings;
    }

    public static Container<JToken> Respond(ConfigurationParams request, JObject settings) =>
        new(request.Items.Select(item => item.Section switch
        {
            "python" => (JToken)settings.DeepClone(),
            null or "" => new JObject { ["python"] = settings.DeepClone() },
            _ => JValue.CreateNull()
        }));
}
