using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform;
using OneWare.Essentials.Services;
using Prism.Ioc;

namespace OneWare.Demo.Browser;

public class WebDemoApp : DemoApp
{
    protected override string GetDefaultLayoutName => "Web";
}