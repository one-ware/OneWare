using System.Linq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace OneWare.Python.UnitTests;

public class PythonLanguageServerConfigurationTests
{
    [Theory]
    [InlineData("/work/project with spaces/.venv/bin/python")]
    [InlineData(@"C:\Projects\my project\.venv\Scripts\python.exe")]
    public void ConfigurationUsesInterpreterAsData(string interpreter)
    {
        var settings = PythonLanguageServerConfiguration.Create(interpreter);

        Assert.Equal(interpreter, settings["pythonPath"]?.Value<string>());
        Assert.Equal("openFilesOnly", settings["pyrefly"]?["diagnosticMode"]?.Value<string>());
        Assert.Null(settings["python"]);
    }

    [Fact]
    public void UnresolvedInterpreterIsOmitted()
    {
        Assert.Null(PythonLanguageServerConfiguration.Create(null)["pythonPath"]);
    }

    [Fact]
    public void ConfigurationResponsesPreserveOrderAndSectionShape()
    {
        var settings = PythonLanguageServerConfiguration.Create("/work/.venv/bin/python");
        var result = PythonLanguageServerConfiguration.Respond(new ConfigurationParams
        {
            Items = new Container<ConfigurationItem>(
                new ConfigurationItem { Section = "unknown" },
                new ConfigurationItem { Section = "python", ScopeUri = "file:///work/" },
                new ConfigurationItem { Section = "python" },
                new ConfigurationItem())
        }, settings).ToArray();

        Assert.Equal(JTokenType.Null, result[0].Type);
        Assert.True(JToken.DeepEquals(settings, result[1]));
        Assert.True(JToken.DeepEquals(settings, result[2]));
        Assert.True(JToken.DeepEquals(settings, result[3]["python"]));
        Assert.NotSame(settings, result[1]);
    }

    [Fact]
    public void PackageHasPinnedTargetsForAllDesktopPlatforms()
    {
        var version = Assert.Single(PythonModule.PyreflyPackage.Versions!);
        Assert.Equal(PythonModule.PyreflyVersion, version.Version);
        Assert.Equal(
            new[] { "linux-arm64", "linux-x64", "osx-arm64", "osx-x64", "win-arm64", "win-x64" },
            version.Targets!.Select(target => target.Target).Order().ToArray());
        foreach (var target in version.Targets!)
        {
            Assert.StartsWith(
                $"https://github.com/facebook/pyrefly/releases/download/{PythonModule.PyreflyVersion}/",
                target.Url);
            var setting = Assert.Single(target.AutoSetting!);
            Assert.Equal(PythonModule.LspPathSetting, setting.SettingKey);
            Assert.Equal(target.Target!.StartsWith("win-") ? "pyrefly.exe" : "pyrefly", setting.RelativePath);
        }
        Assert.Contains(".py", PythonModule.SupportedExtensions);
        Assert.Contains(".pyi", PythonModule.SupportedExtensions);
    }
}
