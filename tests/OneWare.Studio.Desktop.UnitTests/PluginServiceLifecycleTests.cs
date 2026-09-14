using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OneWare.Core.ModuleLogic;
using OneWare.Core.Services;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using Xunit;

namespace OneWare.Studio.Desktop.UnitTests;

public sealed class PluginServiceLifecycleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "oneware-loader-" + Guid.NewGuid().ToString("N"));

    private PluginService CreateLoader()
    {
        var catalog = new OneWareModuleCatalog();
        return new PluginService(catalog, new OneWareModuleManager(catalog), new ModuleServiceRegistry(),
            new TestPaths(Path.Combine(_folder, "session")), null!);
    }

    private string CreatePlugin()
    {
        var path = Path.Combine(_folder, "installed", "Example.Plugin");
        Directory.CreateDirectory(path);
        var reference = PluginCompatibilityChecker.GetReferencedAssembliesRecursive(Assembly.GetEntryAssembly()!)
            .Values.First(x => x.Version != null);
        File.WriteAllText(Path.Combine(path, "compatibility.txt"), $"{reference.Name}:{reference.Version}");
        return path;
    }

    [Theory]
    [InlineData("Example.stage-deadbeef")]
    [InlineData("Example.BACKUP-deadbeef")]
    public void LoaderRejectsCrashArtifactsEvenWhenCalledDirectly(string name)
    {
        var loader = CreateLoader();
        Assert.Throws<InvalidOperationException>(() => loader.AddPlugin(Path.Combine(_folder, name)));
        Assert.Empty(loader.InstalledPlugins);
    }

    [Fact]
    public void RemovingPluginDoesNotForgetProcessLoadIdentity()
    {
        var loader = CreateLoader();
        var path = CreatePlugin();
        var plugin = loader.AddPlugin(path + Path.DirectorySeparatorChar);
        Assert.True(plugin.IsCompatible, plugin.CompatibilityReport);
        Assert.Equal("Example.Plugin", plugin.Id);
        loader.RemovePlugin(plugin);
        Assert.Empty(loader.InstalledPlugins);
        CreatePlugin();
        var error = Assert.Throws<InvalidOperationException>(() => loader.AddPlugin(path));
        Assert.Contains("Restart", error.Message);
        Assert.Empty(loader.InstalledPlugins);
    }

    [Fact]
    public void FailedActivationAlsoReservesProcessLoadIdentity()
    {
        var loader = CreateLoader();
        var path = CreatePlugin();
        // Force copying to fail after compatibility passed and activation began.
        File.WriteAllText(Path.Combine(_folder, "session", "Plugins", "Example.Plugin"), "blocked destination");
        var plugin = loader.AddPlugin(path);
        Assert.False(plugin.IsCompatible);
        loader.RemovePlugin(plugin);
        CreatePlugin();
        Assert.Throws<InvalidOperationException>(() => loader.AddPlugin(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }

    private sealed class TestPaths(string sessionDirectory) : IPaths
    {
        public string SessionDirectory => sessionDirectory;
        public string AppName => "Test";
        public string AppIconPath => "";
        public string AppFolderName => "Test";
        public string AppDataDirectory => sessionDirectory;
        public string TempDirectory => sessionDirectory;
        public string LayoutDirectory => sessionDirectory;
        public string SettingsPath => sessionDirectory;
        public string DocumentsDirectory => sessionDirectory;
        public string CrashReportsDirectory => sessionDirectory;
        public string ProjectsDirectory => sessionDirectory;
        public string PackagesDirectory => sessionDirectory;
        public string NativeToolsDirectory => sessionDirectory;
        public string OnnxRuntimesDirectory => sessionDirectory;
        public string PluginsDirectory => sessionDirectory;
        public string ChangelogUrl => "";
        public string UpdateInfoUrl => "";
    }
}