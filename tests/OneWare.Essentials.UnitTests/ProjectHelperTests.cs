using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class ProjectHelperTests
{
    [Fact]
    public void MathWildCardTests()
    {
        var include = new[] {"**"};
        var exclude = new[] {".git"};
        
        Assert.False(ProjectHelper.MatchWildCards(".git/FETCH_HEAD", include, exclude));
        Assert.True(ProjectHelper.MatchWildCards("test.vhd", include, exclude));
        Assert.True(ProjectHelper.MatchWildCards("nice/test.vhd", include, exclude));
    }
}