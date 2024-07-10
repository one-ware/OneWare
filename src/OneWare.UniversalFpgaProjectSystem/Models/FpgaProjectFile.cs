using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DynamicData;
using OneWare.Essentials.Models;
using OneWare.ProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Models;

public class FpgaProjectFile : ProjectFile
{
    public FpgaProjectFile(string header, IProjectFolder topFolder) : base(header, topFolder)
    {
       
    }
}