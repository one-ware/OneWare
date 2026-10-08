using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Microsoft.Extensions.Logging;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;

namespace OneWare.Core.Services;

public class FileIconService : IFileIconService
{
    private readonly Dictionary<string, IObservable<IImage>> _iconStore = new();
    private readonly ILogger _logger;

    public FileIconService(ILogger logger)
    {
        _logger = logger;

        RegisterFileIcon("FileIcon.File", ".*");

        // HDL and FPGA formats keep OneWare's own icons
        RegisterFileIcon("GhdpFileIcon", ".ghdp");
        RegisterFileIcon("VhdpFileIcon", ".vhdp");
        RegisterFileIcon("VhdlFileIcon", ".vhd", ".vhdl");
        RegisterFileIcon("VerilogFileIcon", ".v", ".vh");
        RegisterFileIcon("QsysFileIcon", ".qsys");
        RegisterFileIcon("SystemVerilogFileIcon", ".sv", ".svh");
        RegisterFileIcon("FileIcon.Tune", ".pcf", ".xdc", ".sdc", ".lpf", ".ucf", ".qsf", ".ccf");
        RegisterFileIcon("FileIcon.Hex", ".hex", ".bin", ".bit", ".mem", ".mif");

        RegisterFileIcon("FileIcon.Arduino", ".ino");
        RegisterFileIcon("FileIcon.JavaScript", ".js", ".mjs", ".cjs", ".jsx");
        RegisterFileIcon("FileIcon.Python", ".py");
        RegisterFileIcon("FileIcon.CSharp", ".cs");
        RegisterFileIcon("FileIcon.C", ".c");
        RegisterFileIcon("FileIcon.H", ".h");
        RegisterFileIcon("FileIcon.Cpp", ".cpp", ".cc", ".cxx");
        RegisterFileIcon("FileIcon.Hpp", ".hpp", ".hh", ".hxx");
        RegisterFileIcon("FileIcon.Tcl", ".tcl");
        RegisterFileIcon("FileIcon.Console", ".sh", ".bash", ".bat", ".cmd", ".ps1");
        RegisterFileIcon("FileIcon.Markdown", ".md");
        RegisterFileIcon("FileIcon.Json", ".json");
        RegisterFileIcon("FileIcon.Xml", ".xml");
        RegisterFileIcon("FileIcon.Yaml", ".yaml", ".yml");
        RegisterFileIcon("FileIcon.Settings", ".ini", ".cfg", ".conf");
        RegisterFileIcon("FileIcon.Document", ".txt");
        RegisterFileIcon("FileIcon.Log", ".log");
        RegisterFileIcon("FileIcon.Table", ".csv", ".tsv");
        RegisterFileIcon("FileIcon.Image", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".webp");
        RegisterFileIcon("FileIcon.Svg", ".svg");
        RegisterFileIcon("FileIcon.Pdf", ".pdf");
        RegisterFileIcon("FileIcon.Zip", ".zip", ".tar", ".gz", ".7z");
        RegisterFileIcon("FileIcon.Audio", ".mp3", ".wav", ".flac", ".ogg");
        RegisterFileIcon("FileIcon.Video", ".mp4", ".avi", ".mov", ".mkv", ".webm");
        RegisterFileIcon("FileIcon.Onnx", ".onnx");
        RegisterFileIcon("FileIcon.Git", ".gitignore", ".gitattributes", ".gitmodules");
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

    public IconModel GetFileIconModel(string extension)
    {
        return new IconModel()
        {
            IconObservable = GetFileIcon(extension)
        };
    }
}