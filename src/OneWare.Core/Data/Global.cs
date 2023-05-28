using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace OneWare.Core.Data
{
    public static class Global
    {
        public static bool SaveLastProjects = true;
        public static string VersionCode => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";

        public static Thickness WindowsOnlyBorder => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new Thickness(1) : new
            Thickness(0);

        public static CornerRadius WindowsCornerRadius => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(8) : new CornerRadius(0))
            : new CornerRadius(0);
        
        public static CornerRadius WindowsCornerRadiusBottom => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0,0,8,8) : new CornerRadius(0))
            : new CornerRadius(0);
        
        public static CornerRadius WindowsCornerRadiusBottomLeft => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0,0,0,8) : new CornerRadius(0))
            : new CornerRadius(0);
        
        public static CornerRadius WindowsCornerRadiusBottomRight => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.OSVersion.Version.Build >= 22000 ? new CornerRadius(0,0,8,0) : new CornerRadius(0))
            : new CornerRadius(0);

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

        public static readonly FilePickerFileType ExeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new FilePickerFileType("Executable (*.exe)")
            {
                Patterns = new[] { "*.exe" }
            } : new FilePickerFileType("Executable (*)") 
            {
                Patterns = new[] { "*.*" }
            };
        
        public static readonly FilePickerFileType AllFiles = new("All files (*)")
        {
            Patterns = new[] { "*.*" }
        };
        
        public static KeyModifiers ControlKey => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? KeyModifiers.Meta
            : KeyModifiers.Control;

        #endregion
    }
}