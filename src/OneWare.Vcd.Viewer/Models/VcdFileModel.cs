using CommunityToolkit.Mvvm.ComponentModel;
using OneWare.Vcd.Parser.Data;

namespace OneWare.Vcd.Viewer.Models;

public class VcdFileModel : ObservableObject
{
    private VcdFile _file;
    public VcdFileModel(VcdFile file)
    {
        
    }
}