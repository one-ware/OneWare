using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;

namespace OneWare.Core.Data;

public static class Global
{
    public static string VersionCode => Assembly.GetEntryAssembly()!.GetName().Version?.ToString() ?? "";

    #region FileDialogFilters

    public static readonly FilePickerFileType VhdpProjFile = new("Project Files (*.vhdpproj)")
    {
        Patterns = new[] { "*.vhdpproj" }
    };

    public static readonly FilePickerFileType VhdpFile = new("VHDP Files (*.vhdp)")
    {
        Patterns = new[] { "*.vhdp" }
    };

    public static readonly FilePickerFileType VhdlFile = new("VHDL Files (*.vhdl)")
    {
        Patterns = new[] { "*.vhd" }
    };

    public static readonly FilePickerFileType VerilogFile = new("Verilog Files (*.v)")
    {
        Patterns = new[] { "*.v" }
    };

    public static readonly FilePickerFileType QsysFile = new("QSYS Files (*.qsys)")
    {
        Patterns = new[] { "*.qsys" }
    };

    public static readonly FilePickerFileType QuartusFile = new("Quartus Files (*.qsf)")
    {
        Patterns = new[] { "*.qsf" }
    };

    public static readonly FilePickerFileType SopcInfoFile = new("SOPCINFO Files (*.sopcinfo)")
    {
        Patterns = new[] { "*.sopcinfo" }
    };

    public static readonly FilePickerFileType QipFile = new("Qip Files (*.qip)")
    {
        Patterns = new[] { "*.qip" }
    };

    public static readonly FilePickerFileType VcdFile = new("VCD Files (*.vcd)")
    {
        Patterns = new[] { "*.vcd" }
    };

    public static readonly FilePickerFileType JpgFile = new("JPG Files (*.jpg)")
    {
        Patterns = new[] { "*.jpg" }
    };

    public static readonly FilePickerFileType ExeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? new FilePickerFileType("Executable (*.exe)")
        {
            Patterns = new[] { "*.exe" }
        }
        : new FilePickerFileType("Executable (*)")
        {
            Patterns = new[] { "*.*" }
        };

    public static readonly FilePickerFileType AllFiles = new("All files (*)")
    {
        Patterns = new[] { "*.*" }
    };

    #endregion
}