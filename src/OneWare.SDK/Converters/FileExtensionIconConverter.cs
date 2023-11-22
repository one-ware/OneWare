using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace OneWare.SDK.Converters
{
    public class FileExtensionIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && Application.Current != null)
            {
                if (!s.StartsWith(".")) s = Path.GetExtension(s);
                return s.ToLower() switch
                {
                    ".ghdp" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "GhdpFileIcon"),
                    ".cpp" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.Cpp16X"),
                    ".c" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.C16X"),
                    ".h" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.HeaderFile16X"),
                    ".hpp" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.HeaderFile16X"),
                    ".cs" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.Cs16X"),
                    ".vhdp" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VhdpFileIcon"),
                    ".vhd" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VhdlFileIcon"),
                    ".vhdl" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VhdlFileIcon"),
                    ".v" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VerilogFileIcon"),
                    ".sv" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "SystemVerilogFileIcon"),
                    ".py" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "FontAwesome.PythonBrands"),
                    ".qsys" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "QsysFileIcon"),
                    ".ino" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "SimpleIcons.Arduino"),
                    ".vcd" => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "Material.Pulse"),
                    _ => Application.Current.FindResource(Application.Current.RequestedThemeVariant, "VsImageLib.File16X")
                };
            }
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}