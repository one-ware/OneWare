using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class StableUpdateVersionTests
{
    private static FakeState CreateState(string installed, params (string Version, bool Prerelease)[] versions)
    {
        var pkg = new Package
        {
            Id = "test",
            Name = "Test",
            Versions = versions
                .Select(x => new PackageVersion { Version = x.Version, IsPrerelease = x.Prerelease })
                .ToArray()
        };

        return new FakeState
        {
            Package = pkg,
            Status = PackageStatus.UpdateAvailablePrerelease,
            InstalledVersion = new PackageVersion { Version = installed }
        };
    }

    [Fact]
    public void PrereleaseOnlyUpdate_IsIgnored()
    {
        var state = CreateState("1.0.0", ("1.0.0", false), ("1.1.0-beta.1", true));

        Assert.Null(state.ResolveStableUpdateVersion());
    }

    [Fact]
    public void NewerStableVersion_IsReturned_EvenWithNewerPrerelease()
    {
        var state = CreateState("1.0.0", ("1.0.0", false), ("1.1.0", false), ("1.2.0-beta.1", true));

        Assert.Equal("1.1.0", state.ResolveStableUpdateVersion()?.Version);
    }

    [Fact]
    public void InstalledPrerelease_UpdatesToNewerStable()
    {
        var state = CreateState("1.1.0-beta.1", ("1.0.0", false), ("1.1.0", false), ("1.1.0-beta.1", true));

        Assert.Equal("1.1.0", state.ResolveStableUpdateVersion()?.Version);
    }

    [Fact]
    public void OlderStableThanInstalledPrerelease_IsIgnored()
    {
        var state = CreateState("1.1.0-beta.2", ("1.0.0", false), ("1.1.0-beta.2", true), ("1.1.0-beta.3", true));

        Assert.Null(state.ResolveStableUpdateVersion());
    }
}
