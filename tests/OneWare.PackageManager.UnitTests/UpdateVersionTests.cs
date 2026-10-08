using OneWare.Essentials.Enums;
using OneWare.Essentials.Models;
using OneWare.Essentials.PackageManager;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class UpdateVersionTests
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

        Assert.Null(state.ResolveUpdateVersion());
    }

    [Fact]
    public void NewerStableVersion_IsReturned_EvenWithNewerPrerelease()
    {
        var state = CreateState("1.0.0", ("1.0.0", false), ("1.1.0", false), ("1.2.0-beta.1", true));

        Assert.Equal("1.1.0", state.ResolveUpdateVersion()?.Version);
    }

    [Fact]
    public void InstalledPrerelease_UpdatesToNewerStable()
    {
        var state = CreateState("1.1.0-beta.1", ("1.0.0", false), ("1.1.0", false), ("1.1.0-beta.1", true));

        Assert.Equal("1.1.0", state.ResolveUpdateVersion()?.Version);
    }

    [Fact]
    public void InstalledPrerelease_UpdatesToNewerPrerelease()
    {
        var state = CreateState("1.1.0-beta.2", ("1.0.0", false), ("1.1.0-beta.2", true), ("1.1.0-beta.3", true));

        Assert.Equal("1.1.0-beta.3", state.ResolveUpdateVersion()?.Version);
    }

    [Fact]
    public void InstalledPrerelease_IsLatest_HasNoUpdate()
    {
        var state = CreateState("1.1.0-beta.3", ("1.0.0", false), ("1.1.0-beta.2", true), ("1.1.0-beta.3", true));

        Assert.Null(state.ResolveUpdateVersion());
    }

    [Fact]
    public void InstalledPrerelease_PrefersNewestAcrossChannels()
    {
        var state = CreateState("1.1.0-beta.1", ("1.1.0-beta.1", true), ("1.1.0", false), ("1.2.0-beta.1", true));

        Assert.Equal("1.2.0-beta.1", state.ResolveUpdateVersion()?.Version);
    }

    [Fact]
    public void TargetVersion_StaysOnStableChannel()
    {
        var state = CreateState("1.0.0", ("1.0.0", false), ("1.1.0-beta.1", true));

        Assert.Equal("1.0.0", state.ResolveTargetVersion()?.Version);
    }

    [Fact]
    public void TargetVersion_FollowsPrereleaseChannel()
    {
        // The installed record carries no prerelease flag, the version suffix identifies the channel
        var state = CreateState("1.1.0-beta.1", ("1.0.0", false), ("1.1.0-beta.1", true), ("1.1.0-beta.2", true));

        Assert.True(state.IsOnPrereleaseChannel());
        Assert.Equal("1.1.0-beta.2", state.ResolveTargetVersion()?.Version);
    }
}
