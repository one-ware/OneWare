using NSubstitute;
using OneWare.Essentials.Enums;
using OneWare.Essentials.Helpers;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using OneWare.Essentials.PackageManager.Compatibility;
using OneWare.Essentials.Services;
using OneWare.PackageManager.Installers;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public sealed class PluginLifecycleTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "oneware-provider-" + Guid.NewGuid().ToString("N"));

    private void CopyAssembly(string relativePath)
    {
        var path = Path.Combine(_folder, relativePath, "provider.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(typeof(PluginLifecycleTests).Assembly.Location, path);
    }

    [Theory]
    [InlineData("lib/net10.0")]
    [InlineData("nested")]
    public void ProviderDiscoveryIncludesNestedManagedAssemblies(string relativePath)
    {
        CopyAssembly(relativePath);
        var providers = PluginCompatibilityChecker.ReadProvidedAssemblies([_folder]);
        Assert.Contains(typeof(PluginLifecycleTests).Assembly.GetName().Name!, providers.Keys);
    }

    [Fact]
    public void ProviderDiscoveryUsesOnlyCurrentRuntimeManagedAssets()
    {
        CopyAssembly(Path.Combine("runtimes", "unsupported-rid", "lib", "net10.0"));
        CopyAssembly(Path.Combine("runtimes", PlatformHelper.PlatformIdentifier, "native"));
        Assert.Empty(PluginCompatibilityChecker.ReadProvidedAssemblies([_folder]));
        CopyAssembly(Path.Combine("runtimes", PlatformHelper.PlatformIdentifier, "lib", "net10.0"));
        Assert.Single(PluginCompatibilityChecker.ReadProvidedAssemblies([_folder]));
    }

    [Fact]
    public void NativeLibrariesDoNotBecomeManagedProviders()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "native.dll"), "not a managed assembly");
        Assert.Empty(PluginCompatibilityChecker.ReadProvidedAssemblies([_folder]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RemovedPluginCannotHotReloadEvenAfterPartialLoadFailure(bool compatible)
    {
        var loader = Substitute.For<IPluginService>();
        var plugin = Substitute.For<IPlugin>();
        plugin.Id.Returns("A");
        plugin.IsCompatible.Returns(compatible);
        loader.InstalledPlugins.Returns([plugin]);
        var installer = new PluginPackageInstaller(Substitute.For<IHttpService>(), loader);
        var context = new PackageInstallContext(new Package { Id = "A", Type = "Plugin" },
            new PackageVersion { Version = "1" }, new PackageTarget { Target = "all" }, _folder, new Progress<float>());
        Assert.Equal(PackageStatus.NeedRestart, (await installer.RemoveAsync(context)).Status);
        Assert.Equal(PackageStatus.NeedRestart, (await installer.InstallAsync(context)).Status);
        loader.DidNotReceive().AddPlugin(Arg.Any<string>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
    }
}