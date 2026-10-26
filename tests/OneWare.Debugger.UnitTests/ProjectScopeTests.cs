using System.IO;
using OneWare.Debugger.Helpers;
using Xunit;

namespace OneWare.Debugger.UnitTests;

public class ProjectScopeTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "proj");

    [Fact]
    public void FilesBelowTheRootBelongToTheProject()
    {
        Assert.True(ProjectScope.Contains(Root, Path.Combine(Root, "src", "main.c")));
        Assert.True(ProjectScope.Contains(Root + Path.DirectorySeparatorChar, Path.Combine(Root, "main.c")));
    }

    [Fact]
    public void SiblingWithSamePrefixDoesNotBelong()
    {
        Assert.False(ProjectScope.Contains(Root, Path.Combine(Path.GetTempPath(), "proj2", "main.c")));
    }

    [Fact]
    public void NoProjectContainsNothing()
    {
        Assert.False(ProjectScope.Contains(null, Path.Combine(Root, "main.c")));
    }
}
