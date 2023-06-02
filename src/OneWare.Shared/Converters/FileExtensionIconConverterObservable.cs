using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OneWare.Shared.Enums;

namespace OneWare.Shared.Converters
{
    public class FileExtensionIconConverterObservable : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string s && Application.Current != null)
                return s switch
                {
                    ".ghdp" => Application.Current.GetResourceObservable("GhdpFileIcon"),
                    ".cpp" => Application.Current.GetResourceObservable("VsImageLib.Cpp16X"),
                    ".c" => Application.Current.GetResourceObservable("VsImageLib.C16X"),
                    ".h" => Application.Current.GetResourceObservable("VsImageLib.HeaderFile16X"),
                    ".hpp" => Application.Current.GetResourceObservable("VsImageLib.HeaderFile16X"),
                    ".cs" => Application.Current.GetResourceObservable("VsImageLib.Cs16X"),
                    ".vhdp" => Application.Current.GetResourceObservable("VhdpFileIcon"),
                    ".vhd" => Application.Current.GetResourceObservable("VhdlFileIcon"),
                    ".vhdl" => Application.Current.GetResourceObservable("VhdlFileIcon"),
                    ".v" => Application.Current.GetResourceObservable("VerilogFileIcon"),
                    ".sv" => Application.Current.GetResourceObservable("SystemVerilogFileIcon"),
                    ".py" => Application.Current.GetResourceObservable("FontAwesome.PythonBrands"),
                    ".qsys" => Application.Current.GetResourceObservable("QsysFileIcon"),
                    ".ino" => Application.Current.GetResourceObservable("SimpleIcons.Arduino"),
                    ".vcd" => Application.Current.GetResourceObservable("Material.Pulse"),
                    _ => Application.Current.GetResourceObservable("VsImageLib.File16X")
                };
            return null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}