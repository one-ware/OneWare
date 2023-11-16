using Avalonia.Platform;
using Jint;
using OneWare.Shared.Models;
using OneWare.Shared.Services;

namespace OneWare.NetListSvgIntegration.Services;

public class NetListSvgService
{
    public NetListSvgService(ILogger logger)
    {
        try
        {
            var engine = new Engine();
            
            var console = new
            {
                log = new Action<object>(x => Console.WriteLine(x)),
                warn = new Action<object>(x => Console.WriteLine("WARN: " + x)),
                error = new Action<object>(x => Console.WriteLine("ERROR: " + x))
            };
            engine.SetValue("console", console);
            engine.Execute("const window = {};");
            engine.Execute("window.Math = Math");
            engine.Execute("window.Array = Array");
            engine.Execute("console.log(window.Math.sqrt(9));"); 
            engine.SetValue("window.Array", engine.GetValue("Array"));
            engine.SetValue("window.Date", new Func<string, object>((input) => DateTime.Parse(input)));
            engine.Execute("const exports = {}");
            engine.SetValue("setTimeout", new Action<Action, int>((action, delay) =>
            {
                if (delay <= 0)
                {
                    action();
                    return;
                }
                var timer = new System.Timers.Timer(delay);
                timer.Elapsed += (sender, args) =>
                {
                    action();
                    timer.Stop();
                };
                timer.Start();
            }));
            
            ExecuteScript(engine, "avares://OneWare.NetListSvgIntegration/Assets/elk.bundled.js");
            ExecuteScript(engine, "avares://OneWare.NetListSvgIntegration/Assets/netlistsvg.bundle.js");
            
            engine.Execute("var netlistsvg = window.netlistsvg");
            
            engine.Execute("netlistsvg.render(netlistsvg.digitalSkin, netlistsvg.exampleDigital, (err, result) => console.log(result));");
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
    }

    private void ExecuteScript(Engine engine, string resource)
    {
        using var stream = AssetLoader.Open(new Uri(resource));
        using var reader = new StreamReader(stream);
        var script = reader.ReadToEnd();
        engine.Execute(script);
    }
    
    public async Task CreateFromJsonAsync(IProjectFile jsonFile)
    {
        
    }
}