using OneWare.SourceControl.Models;
using Xunit;

namespace OneWare.SourceControl.UnitTests;

public class GitGraphBuilderTests
{
    private static GitCommitInfo Commit(string sha, params string[] parents)
    {
        return new GitCommitInfo(sha, sha, sha, "Author", "author@example.invalid", DateTimeOffset.Now, parents);
    }

    [Fact]
    public void LinearHistoryStaysInFirstLane()
    {
        var rows = new GitGraphBuilder().Append([Commit("c", "b"), Commit("b", "a"), Commit("a")]);

        Assert.All(rows, row => Assert.Equal(0, row.Lane));
        Assert.All(rows, row => Assert.Equal(1, row.LaneCount));
        Assert.Equal([new GitGraphLine(GitGraphLineKind.Outgoing, 0, 0, 0)], rows[0].Lines);
        Assert.Equal(
            [new GitGraphLine(GitGraphLineKind.Incoming, 0, 0, 0), new GitGraphLine(GitGraphLineKind.Outgoing, 0, 0, 0)],
            rows[1].Lines);
        Assert.Equal([new GitGraphLine(GitGraphLineKind.Incoming, 0, 0, 0)], rows[2].Lines);
        Assert.Empty(rows[2].Continuation);
    }

    [Fact]
    public void MergeOpensSecondLaneThatJoinsAtTheForkPoint()
    {
        var rows = new GitGraphBuilder().Append(
        [
            Commit("merge", "left", "right"),
            Commit("left", "base"),
            Commit("right", "base"),
            Commit("base")
        ]);

        var merge = rows[0];
        Assert.True(merge.IsMerge);
        Assert.Equal(0, merge.Lane);
        Assert.Contains(new GitGraphLine(GitGraphLineKind.Outgoing, 0, 0, 0), merge.Lines);
        Assert.Contains(merge.Lines, x => x is { Kind: GitGraphLineKind.Outgoing, From: 0, To: 1 });
        Assert.Equal(2, merge.LaneCount);

        var left = rows[1];
        Assert.Equal(0, left.Lane);
        Assert.Contains(left.Lines, x => x is { Kind: GitGraphLineKind.PassThrough, From: 1, To: 1 });

        var right = rows[2];
        Assert.Equal(1, right.Lane);
        Assert.NotEqual(left.Color, right.Color);
        Assert.Contains(right.Lines, x => x is { Kind: GitGraphLineKind.PassThrough, From: 0, To: 0 });

        var fork = rows[3];
        Assert.Equal(0, fork.Lane);
        Assert.Contains(fork.Lines, x => x is { Kind: GitGraphLineKind.Incoming, From: 0, To: 0 });
        Assert.Contains(fork.Lines, x => x is { Kind: GitGraphLineKind.Incoming, From: 1, To: 0 });
        Assert.Empty(fork.Continuation);
    }

    [Fact]
    public void BranchLaneKeepsItsColumnWhileOtherLanesEnd()
    {
        // Two branch tips, the first one ends early (root), the second must not shift into its lane.
        var rows = new GitGraphBuilder().Append(
        [
            Commit("a2", "a1"),
            Commit("b2", "b1"),
            Commit("a1"),
            Commit("b1")
        ]);

        Assert.Equal(0, rows[0].Lane);
        Assert.Equal(1, rows[1].Lane);
        Assert.Equal(0, rows[2].Lane);
        Assert.Equal(1, rows[3].Lane);
        Assert.Contains(rows[3].Lines, x => x is { Kind: GitGraphLineKind.Incoming, From: 1, To: 1 });
    }

    [Fact]
    public void IncrementalPagesMatchASingleLayout()
    {
        GitCommitInfo[] commits =
        [
            Commit("m", "a", "x"),
            Commit("a", "b"),
            Commit("x", "b"),
            Commit("b", "c"),
            Commit("c")
        ];

        var single = new GitGraphBuilder().Append(commits);

        var paged = new GitGraphBuilder();
        var rows = paged.Append(commits.Take(2)).Concat(paged.Append(commits.Skip(2))).ToArray();

        Assert.Equal(single.Select(x => (x.Lane, x.Color, x.LaneCount)), rows.Select(x => (x.Lane, x.Color, x.LaneCount)));
        Assert.Equal(single.Select(x => x.Lines), rows.Select(x => x.Lines), new LinesComparer());
    }

    [Fact]
    public void MarksHeadAndAttachesRefs()
    {
        var refs = new Dictionary<string, IReadOnlyList<GitRefInfo>>
        {
            ["b"] = [new GitRefInfo("main", GitRefKind.LocalBranch, true)]
        };
        var rows = new GitGraphBuilder().Append([Commit("b", "a"), Commit("a")],
            c => refs.TryGetValue(c.Sha, out var list) ? list : [], "b");

        Assert.True(rows[0].IsHead);
        Assert.True(rows[0].HasRefs);
        Assert.False(rows[1].IsHead);
        Assert.False(rows[1].HasRefs);
    }

    private sealed class LinesComparer : IEqualityComparer<IReadOnlyList<GitGraphLine>>
    {
        public bool Equals(IReadOnlyList<GitGraphLine>? x, IReadOnlyList<GitGraphLine>? y)
        {
            return x != null && y != null && x.SequenceEqual(y);
        }

        public int GetHashCode(IReadOnlyList<GitGraphLine> obj) => obj.Count;
    }
}
