using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem;
using OneWare.Vcd.Viewer.ViewModels;
using Prism.Ioc;

namespace OneWare.Studio.Browser;

public class WebStudioApp : StudioApp
{
    protected override string GetDefaultLayoutName => "Web";
}