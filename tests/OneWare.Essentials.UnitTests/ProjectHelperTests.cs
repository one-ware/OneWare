﻿using OneWare.Essentials.Helpers;
using Xunit;

namespace OneWare.Essentials.UnitTests;

public class ProjectHelperTests
{
[Fact]
public void MathWildCardTests()
{
    var include = new[] { "**" };
    var exclude = new[] { ".git" };

    // Test cases for .git directory
    Assert.False(ProjectHelper.MatchWildCards(".git/FETCH_HEAD", include, exclude));
    Assert.False(ProjectHelper.MatchWildCards(".git/HEAD", include, exclude));
    Assert.False(ProjectHelper.MatchWildCards(".git/config", include, exclude));

    // Test cases for files outside .git directory
    Assert.True(ProjectHelper.MatchWildCards("test.vhd", include, exclude));
    Assert.True(ProjectHelper.MatchWildCards("nice/test.vhd", include, exclude));
    Assert.True(ProjectHelper.MatchWildCards("src/OneWare.Core/Helpers/ProjectHelper.cs", include, exclude));

    // Test cases for nested directories outside .git directory
    Assert.True(ProjectHelper.MatchWildCards("tests/OneWare.Essentials.UnitTests/ProjectHelperTests.cs", include, exclude));
    Assert.True(ProjectHelper.MatchWildCards("docs/README.md", include, exclude));

    // Test cases with multiple exclude patterns
    var multiExclude = new[] { ".git", "node_modules" };
    Assert.False(ProjectHelper.MatchWildCards(".git/FETCH_HEAD", include, multiExclude));
    Assert.False(ProjectHelper.MatchWildCards("node_modules/some-package/index.js", include, multiExclude));

    // Test cases with no exclude patterns
    var noExclude = new string[] { };
    Assert.True(ProjectHelper.MatchWildCards(".git/FETCH_HEAD", include, noExclude));
    Assert.True(ProjectHelper.MatchWildCards("test.vhd", include, noExclude));
}
}
