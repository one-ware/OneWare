using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class FileIconService : IFileIconService
{
    private readonly Dictionary<string, IObservable<IImage>> _iconStore = new();
    private readonly ILogger _logger;

    public FileIconService(ILogger logger)
    {
        _logger = logger;

        RegisterFileIcon("VsImageLib.File16X", ".*");
        RegisterFileIcon("GhdpFileIcon", ".ghdp");
        RegisterFileIcon("VhdpFileIcon", ".vhdp");
        RegisterFileIcon("VhdlFileIcon", ".vhd", ".vhdl");
        RegisterFileIcon("VerilogFileIcon", ".v");
        RegisterFileIcon("QsysFileIcon", ".qsys");
        RegisterFileIcon("SystemVerilogFileIcon", ".sv");
        RegisterFileIcon("SimpleIcons.Arduino", ".ino");
        RegisterFileIcon("Material.Pulse", ".vcd", ".ghw", ".fst");
        RegisterFileIcon("Ionicons.LogoJavascript", ".js");
        RegisterFileIcon("FontAwesome.PythonBrands", ".py");
        RegisterFileIcon("VsImageLib.Cs16X", ".cs");
        RegisterFileIcon("VsImageLib.C16X", ".c");
        RegisterFileIcon("VsImageLib.HeaderFile16X", ".h", ".hpp");
        RegisterFileIcon("VsImageLib.Cpp16X", ".cpp");
        RegisterFileIcon("VSImageLib.MarkdownFile_16x", ".md");
        RegisterFileIcon("VSImageLib2019.JSONScript_16x", ".json");
    }

    public void RegisterFileIcon(IObservable<IImage> icon, params string[] extensions)
    {
        foreach (var ext in extensions) _iconStore[ext] = icon;
    }

    public void RegisterFileIcon(string resourceName, params string[] extensions)
    {
        try
        {
            var observable = Application.Current!.GetResourceObservable(resourceName)!.Cast<IImage>();
            RegisterFileIcon(observable, extensions);
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public IObservable<IImage> GetFileIcon(string extension)
    {
        if (_iconStore.TryGetValue(extension, out var observable)) return observable;
        return _iconStore[".*"];
    }
}