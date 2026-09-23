using OneWare.PackageManager.ViewModels;
using Xunit;

namespace OneWare.PackageManager.UnitTests;

public class PackageListComparerTests
{
    [Fact]
    public void GetRelevance_WithoutFilter_IsAlwaysEqual()
    {
        Assert.Equal(0, PackageListComparer.GetRelevance("Anything", string.Empty));
        Assert.Equal(0, PackageListComparer.GetRelevance(null, string.Empty));
    }

    [Theory]
    [InlineData("Quartus", "quartus", 0)]
    [InlineData("Quartus Prime", "quartus", 1)]
    [InlineData("Intel Quartus", "quartus", 2)]
    [InlineData("Vivado", "quartus", 3)]
    public void GetRelevance_RanksExactBeforePrefixBeforeContains(string name, string filter, int expected)
    {
        Assert.Equal(expected, PackageListComparer.GetRelevance(name, filter));
    }

    [Fact]
    public void GetRelevance_IsCaseInsensitive()
    {
        Assert.Equal(0, PackageListComparer.GetRelevance("QUARTUS", "quartus"));
        Assert.Equal(1, PackageListComparer.GetRelevance("quartus prime", "QUARTUS"));
    }

    [Fact]
    public void GetRelevance_WithMissingName_RanksLast()
    {
        Assert.Equal(3, PackageListComparer.GetRelevance(null, "quartus"));
        Assert.Equal(3, PackageListComparer.GetRelevance(string.Empty, "quartus"));
    }
}
