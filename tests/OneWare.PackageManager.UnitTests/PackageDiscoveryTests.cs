using Microsoft.Extensions.Logging;
using NSubstitute;
using OneWare.Essentials.PackageManager;
using OneWare.PackageManager.Services;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageDiscoveryTests
{
    [Theory]
    [InlineData("ONE AI", 0)]
    [InlineData("oneware.ai", 0)]
    [InlineData("ai", 1)]
    [InlineData("vision Tools", 2)]
    [InlineData("unknown", null)]
    public void TokenSearchAndRanking(string query, int? score)
        => Assert.Equal(score, PackageSearch.Score(new Package { Id = "OneWare.AI", Name = "ONE AI", Description = "Computer vision", Category = "Tools" }, query));

    [Fact]
    public async Task FeaturingRequiresWinningOfficialSourceAndNeverCopilot()
    {
        var client = Substitute.For<IPackageRepositoryClient>();
        var ai = new Package { Id = "OneWare.AI", Name = "ONE AI" };
        client.LoadRepositoryAsync("official", Arg.Any<CancellationToken>()).Returns(new[] { ai, new Package { Id = "copilotcli" } });
        client.LoadRepositoryAsync("custom", Arg.Any<CancellationToken>()).Returns(new[] { ai });
        var catalog = new PackageCatalog(client, Substitute.For<ILogger>());
        catalog.RegisterOfficialSource("official");
        await catalog.RefreshAsync([["official"]]);
        Assert.Equal(["OneWare.AI"], catalog.FeaturedPackageIds);
        await catalog.RefreshAsync([["official"], ["custom"]]);
        Assert.Empty(catalog.FeaturedPackageIds);
        await catalog.RefreshAsync([["custom"]]);
        Assert.Empty(catalog.FeaturedPackageIds);
        await catalog.RefreshAsync([["offline"]]);
        Assert.Empty(catalog.FeaturedPackageIds);
    }
}