using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Converters
{
    public class FileExtensionIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && Application.Current != null)
                return s switch
                {
                    ".ghdp" => Application.Current.FindResource("GhdpFileIcon"),
                    ".cpp" => Application.Current.FindResource("VsImageLib.Cpp16X"),
                    ".c" => Application.Current.FindResource("VsImageLib.C16X"),
                    ".h" => Application.Current.FindResource("VsImageLib.HeaderFile16X"),
                    ".hpp" => Application.Current.FindResource("VsImageLib.HeaderFile16X"),
                    ".cs" => Application.Current.FindResource("VsImageLib.Cs16X"),
                    ".vhdp" => Application.Current.FindResource("VhdpFileIcon"),
                    ".vhd" => Application.Current.FindResource("VhdlFileIcon"),
                    ".vhdl" => Application.Current.FindResource("VhdlFileIcon"),
                    ".v" => Application.Current.FindResource("VerilogFileIcon"),
                    ".sv" => Application.Current.FindResource("SystemVerilogFileIcon"),
                    ".py" => Application.Current.FindResource("FontAwesome.PythonBrands"),
                    ".qsys" => Application.Current.FindResource("QsysFileIcon"),
                    ".ino" => Application.Current.FindResource("SimpleIcons.Arduino"),
                    ".vcd" => Application.Current.FindResource("Material.Pulse"),
                    _ => Application.Current.FindResource("VsImageLib.File16X")
                };
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}