using System.IO;
using OneWare.Core.Services;
using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public class PluginNativeLibraryResolverTests
{
    [Fact]
    public void GetCandidatePaths_PrioritizesPluginRuntimeNativeDirectory()
    {
        var pluginPath = Path.Combine(Path.GetTempPath(), "OneWarePlugin");
        var applicationBaseDirectory = Path.Combine(Path.GetTempPath(), "OneWareApplication");
        var libraryFileName = PlatformHelper.GetLibraryFileName("example");

        var paths = PluginNativeLibraryResolver.GetCandidatePaths(
            pluginPath,
            "example",
            applicationBaseDirectory);

        Assert.Equal(
            Path.Combine(pluginPath, "runtimes", PlatformHelper.PlatformIdentifier, "native", libraryFileName),
            paths[0]);
        Assert.Equal(Path.Combine(pluginPath, libraryFileName), paths[2]);
        Assert.Equal(Path.Combine(applicationBaseDirectory, libraryFileName), paths[4]);
    }
}
