using Avalonia.Platform;
using AvaloniaEdit.Document;
using Jint;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.EditorExtensions;
using OneWare.Essentials.Services;

namespace OneWare.Vhdl.Formatting;

public class VhdlFormatter : IFormattingStrategy
{
    public void Format(TextDocument document)
    {
        var test = Format(document.Text);
        if (test != null)
            document.Text = test;
    }

    private static string? Format(string source)
    {
        try
        {
            using var engine = new Engine();

            using var stream = AssetLoader.Open(new Uri("avares://OneWare.Vhdl/Assets/formatter.js"));
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();

            engine.Execute("const exports = {}");
            engine.Execute(script);

            var settings = new BeautifierSettings
            {
                KeywordCase = "UPPERCASE",
                TypeNameCase = "UPPERCASE",
                EndOfLine = "\n",
                Indentation = "    ",
                AddNewLine = true
                // NewLineSettings = new NewLineSettings
                // {
                //     newLineAfter = new []
                //     {
                //         ";",
                //         "then"
                //     },
                //     noLineAfter = Array.Empty<string>()
                //}
            };

            engine.SetValue("settings", settings);
            engine.SetValue("source", source);
            engine.Execute("var formatted = beautify(source, settings)");
            var result = engine.GetValue("formatted").ToObject() as string;
            return result;
        }
        catch (Exception e)
        {
            ContainerLocator.Container.Resolve<ILogger>().Error(e.Message, e);
            return null;
        }
    }
}

public class BeautifierSettings
{
    public string? KeywordCase { get; set; }

    public string? TypeNameCase { get; set; }

    public string? EndOfLine { get; set; }

    public string? Indentation { get; set; }

    public bool AddNewLine { get; set; }

    public NewLineSettings? NewLineSettings { get; set; }
}

public class NewLineSettings
{
    // ReSharper disable once InconsistentNaming
    public string[]? newLineAfter { get; set; }

    // ReSharper disable once InconsistentNaming
    public string[]? noLineAfter { get; set; }
}