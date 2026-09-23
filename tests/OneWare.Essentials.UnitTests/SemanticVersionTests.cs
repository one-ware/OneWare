using Xunit;
using OneWare.Essentials.PackageManager;

namespace OneWare.Essentials.UnitTests;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("1.2.3")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    [InlineData("7.0.0-dev.20260707.2")]
    [InlineData("7.0.1-rc")]
    [InlineData("1.2.3-beta.1+build.5")]
    public void TryParse_AcceptsSupportedFormats(string version)
    {
        Assert.True(SemanticVersion.TryParse(version, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("1.x.3")]
    [InlineData("1.2.3-")]
    public void TryParse_RejectsInvalidFormats(string? version)
    {
        Assert.False(SemanticVersion.TryParse(version, out var parsed));
        Assert.Equal(SemanticVersion.Empty, parsed);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.4")]
    [InlineData("1.2.3", "1.3.0")]
    [InlineData("1.2.3.1", "1.2.3.2")]
    [InlineData("7.0.0-dev.20260707.2", "7.0.0")]
    [InlineData("7.0.0-dev.20260706.1", "7.0.0-dev.20260707.2")]
    [InlineData("7.0.0-alpha", "7.0.0-beta")]
    [InlineData("7.0.0-rc.1", "7.0.0-rc.1.1")]
    [InlineData("7.0.0-1", "7.0.0-alpha")]
    [InlineData("7.0.0-dev.20260707.2", "7.0.2")]
    public void CompareTo_OrdersVersions(string lower, string higher)
    {
        Assert.True(SemanticVersion.TryParse(lower, out var l));
        Assert.True(SemanticVersion.TryParse(higher, out var h));

        Assert.True(h > l);
        Assert.True(l < h);
        Assert.NotEqual(l, h);
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3.0")]
    [InlineData("1.2", "1.2.0.0")]
    [InlineData("1.2.3-beta.1+build.5", "1.2.3-beta.1")]
    public void CompareTo_TreatsEquivalentVersionsAsEqual(string left, string right)
    {
        Assert.True(SemanticVersion.TryParse(left, out var l));
        Assert.True(SemanticVersion.TryParse(right, out var r));

        Assert.Equal(l, r);
    }

    [Fact]
    public void IsPrerelease_DetectsSuffix()
    {
        Assert.True(SemanticVersion.TryParse("7.0.0-dev.20260707.2", out var prerelease));
        Assert.True(SemanticVersion.TryParse("7.0.2", out var stable));

        Assert.True(prerelease.IsPrerelease);
        Assert.False(stable.IsPrerelease);
    }
}
