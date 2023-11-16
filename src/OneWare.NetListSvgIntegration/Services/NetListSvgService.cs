using System.Drawing;
using System.Net.Mime;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using ImTools;
using Jint;
using OneWare.Shared.Enums;
using OneWare.Shared.Models;
using OneWare.Shared.Services;
using Prism.Ioc;
using Color = Avalonia.Media.Color;

namespace OneWare.NetListSvgIntegration.Services;

public class NetListSvgService
{
    private readonly ILogger _logger;
    private readonly IActive _active;
    private readonly IDockService _dockService;
    private bool _isRecording;

    private string _output = string.Empty;

    private Engine? _engine;
    
    public NetListSvgService(ILogger logger, IActive active, IDockService dockService)
    {
        _logger = logger;
        _active = active;
        _dockService = dockService;
    }

    private void PrepareEngine()
    {
        try
        {
            _engine = new Engine();
            
            var console = new
            {
                log = new Action<object>(x =>
                {
                    if(_isRecording) _output += x;
                }),
                warn = new Action<object>(x => _logger.Warning(x.ToString() ?? "")),
                error = new Action<object>(x => _logger.Error(x.ToString() ?? ""))
            };
            _engine.SetValue("console", console);
            _engine.Execute("const window = {};");
            _engine.Execute("window.Math = Math");
            _engine.Execute("window.Array = Array");
            _engine.SetValue("window.Array", _engine.GetValue("Array"));
            _engine.SetValue("window.Date", new Func<string, object>((input) => DateTime.Parse(input)));
            _engine.Execute("const exports = {}");
            _engine.SetValue("setTimeout", new Action<Action, int>((action, delay) =>
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

            var script1 = GetAvaloniaAsset("avares://OneWare.NetListSvgIntegration/Assets/elk.bundled.js");
            _engine.Execute(script1);
            var script2 = GetAvaloniaAsset("avares://OneWare.NetListSvgIntegration/Assets/netlistsvg.bundle.js");
            _engine.Execute(script2);
            
            _engine.Execute("var netlistsvg = window.netlistsvg");
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public async Task ShowSchemeAsync(IProjectFile jsonFile)
    {
        var svgStr = await CreateFromJsonAsync(jsonFile);
        
        if (!string.IsNullOrEmpty(svgStr))
        {
           
            var newFileName = Path.GetFileNameWithoutExtension(jsonFile.FullPath) + ".svg";
            var newFile = jsonFile.TopFolder!.AddFile(newFileName, true);
            await File.WriteAllTextAsync(newFile.FullPath, svgStr);
            await _dockService.OpenFileAsync(newFile);
        }
    }
    
    public async Task<string?> CreateFromJsonAsync(IProjectFile jsonFile)
    {
        _isRecording = true;
        _output = string.Empty;

        var state = _active.AddState("Rendering Scheme...", AppState.Loading);

        var theme = Application.Current!.ActualThemeVariant;
        var skin = LoadSkin(theme);
        var backgroundHex = Application.Current!.FindResource(theme, "ThemeControlLowColor") is Color background ? 
            $"#{background.R:X2}{background.G:X2}{background.B:X2}" : "#FFFFFF";

        try
        {
            await Task.Run(() =>
            {
                if (_engine is null) PrepareEngine();
                if (_engine is null) throw new NullReferenceException(nameof(_engine));
                
                var json = File.ReadAllText(jsonFile.FullPath);
                
                _engine.SetValue("OneWareJsonData", json);
                _engine.Execute("var OneWareNetList = JSON.parse(OneWareJsonData)");
                
                _engine.SetValue("OneWareSkin", skin);
                _engine.Execute(
                    "netlistsvg.render(OneWareSkin, OneWareNetList, (err, result) => console.log(result));");

                if (theme != ThemeVariant.Light)
                {
                    _output = _output.Replace("fill: white; stroke: none", $"fill: {backgroundHex}; stroke: none");
                    _output = _output.Replace("fill:#000", $"fill:#FFF");
                }
            });
        }  
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
        
        _active.RemoveState(state);
        
        _isRecording = false;
        return _output;
    }

    private string GetAvaloniaAsset(string resource)
    {
        using var stream = AssetLoader.Open(new Uri(resource));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string LoadSkin(ThemeVariant themeVariant)
    {
        if (themeVariant == ThemeVariant.Light)
        {
            return GetAvaloniaAsset("avares://OneWare.NetListSvgIntegration/Assets/theme_teros.svg");
            //return GetAvaloniaAsset("avares://OneWare.NetListSvgIntegration/Assets/theme_light.svg");
        }
        else
        {
            return GetAvaloniaAsset("avares://OneWare.NetListSvgIntegration/Assets/theme_dark.svg");
        }
    }
}