using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneWare.SourceControl.Models;

public enum GitRefKind
{
    Head,
    LocalBranch,
    RemoteBranch,
    Tag
}

public sealed record GitRefInfo(string Name, GitRefKind Kind, bool IsCurrent = false);

public sealed record GitCommitInfo(
    string Sha,
    string MessageShort,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTimeOffset When,
    IReadOnlyList<string> Parents)
{
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}

public enum GitGraphLineKind
{
    /// <summary>Upper half: from the top edge at <see cref="GitGraphLine.From" /> to the node center.</summary>
    Incoming,

    /// <summary>Lower half: from the node center to the bottom edge at <see cref="GitGraphLine.To" />.</summary>
    Outgoing,

    /// <summary>A lane that passes through the row without touching the node.</summary>
    PassThrough
}

public readonly record struct GitGraphLine(GitGraphLineKind Kind, int From, int To, int Color);

public readonly record struct GitGraphLane(int Lane, int Color);

public class GitGraphRow : ObservableObject
{
    public GitGraphRow(GitCommitInfo commit, int lane, int color, IReadOnlyList<GitGraphLine> lines,
        IReadOnlyList<GitGraphLane> continuation, int laneCount)
    {
        Commit = commit;
        Lane = lane;
        Color = color;
        Lines = lines;
        Continuation = continuation;
        LaneCount = laneCount;
    }

    public GitCommitInfo Commit { get; }

    public int Lane { get; }

    public int Color { get; }

    public IReadOnlyList<GitGraphLine> Lines { get; }

    /// <summary>Lanes that continue below this row, used to extend the graph through expanded details.</summary>
    public IReadOnlyList<GitGraphLane> Continuation { get; }

    public int LaneCount { get; }

    public bool IsMerge => Commit.Parents.Count > 1;

    public IReadOnlyList<GitRefInfo> Refs { get; init; } = [];

    public bool IsHead { get; init; }

    public bool HasRefs => Refs.Count > 0;

    public bool IsExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsLoadingFiles
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ObservableCollection<GitCommitFileModel> Files { get; } = [];

    public string AuthorAndDate => $"{Commit.AuthorName}, {FormatRelative(Commit.When)}";

    public string ToolTip =>
        $"{Commit.AuthorName} <{Commit.AuthorEmail}>\n{Commit.When.LocalDateTime:g}\n{Commit.Sha}\n\n{Commit.Message.TrimEnd()}";

    public static string FormatRelative(DateTimeOffset when)
    {
        var span = DateTimeOffset.Now - when;
        if (span.TotalSeconds < 0) return when.LocalDateTime.ToString("d");
        if (span.TotalMinutes < 1) return "now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} hr ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} days ago";
        if (span.TotalDays < 31) return $"{(int)(span.TotalDays / 7)} wk ago";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30.4)} mo ago";
        return $"{(int)(span.TotalDays / 365.25)} yr ago";
    }
}

/// <summary>
///     Assigns commits (in topological order, children before parents) to lanes.
///     Lanes are never compacted, so a branch keeps a straight vertical line until it ends.
///     The builder is incremental so further pages of history continue the existing layout.
/// </summary>
public sealed class GitGraphBuilder
{
    private readonly List<(string Sha, int Color)?> _lanes = [];
    private readonly int _paletteSize;
    private int _nextColor;

    public GitGraphBuilder(int paletteSize = GitGraphPalette.Size)
    {
        _paletteSize = Math.Max(1, paletteSize);
    }

    public IReadOnlyList<GitGraphRow> Append(IEnumerable<GitCommitInfo> commits,
        Func<GitCommitInfo, IReadOnlyList<GitRefInfo>>? refs = null, string? headSha = null)
    {
        var rows = new List<GitGraphRow>();
        foreach (var commit in commits)
        {
            var lines = new List<GitGraphLine>();

            var matching = new List<int>();
            for (var i = 0; i < _lanes.Count; i++)
                if (_lanes[i]?.Sha == commit.Sha)
                    matching.Add(i);

            int lane, color;
            if (matching.Count > 0)
            {
                lane = matching[0];
                color = _lanes[lane]!.Value.Color;
            }
            else
            {
                lane = AllocateLane();
                color = NextColor();
            }

            var widthBefore = _lanes.Count;

            for (var i = 0; i < _lanes.Count; i++)
            {
                if (_lanes[i] is not { } entry) continue;
                lines.Add(matching.Contains(i)
                    ? new GitGraphLine(GitGraphLineKind.Incoming, i, lane, entry.Color)
                    : new GitGraphLine(GitGraphLineKind.PassThrough, i, i, entry.Color));
            }

            foreach (var i in matching) _lanes[i] = null;

            if (commit.Parents.Count > 0)
            {
                EnsureLane(lane);
                _lanes[lane] = (commit.Parents[0], color);
                lines.Add(new GitGraphLine(GitGraphLineKind.Outgoing, lane, lane, color));

                for (var p = 1; p < commit.Parents.Count; p++)
                {
                    var parent = commit.Parents[p];
                    var target = _lanes.FindIndex(x => x?.Sha == parent);
                    int targetColor;
                    if (target >= 0)
                    {
                        targetColor = _lanes[target]!.Value.Color;
                    }
                    else
                    {
                        target = AllocateLane();
                        targetColor = NextColor();
                        _lanes[target] = (parent, targetColor);
                    }

                    lines.Add(new GitGraphLine(GitGraphLineKind.Outgoing, lane, target, targetColor));
                }
            }

            var widthAfter = _lanes.Count;
            while (_lanes.Count > 0 && _lanes[^1] == null) _lanes.RemoveAt(_lanes.Count - 1);

            var continuation = new List<GitGraphLane>();
            for (var i = 0; i < _lanes.Count; i++)
                if (_lanes[i] is { } entry)
                    continuation.Add(new GitGraphLane(i, entry.Color));

            rows.Add(new GitGraphRow(commit, lane, color, lines, continuation,
                Math.Max(Math.Max(widthBefore, widthAfter), lane + 1))
            {
                Refs = refs?.Invoke(commit) ?? [],
                IsHead = commit.Sha == headSha
            });
        }

        return rows;
    }

    private int AllocateLane()
    {
        var free = _lanes.FindIndex(x => x == null);
        if (free >= 0) return free;
        _lanes.Add(null);
        return _lanes.Count - 1;
    }

    private void EnsureLane(int lane)
    {
        while (_lanes.Count <= lane) _lanes.Add(null);
    }

    private int NextColor()
    {
        var color = _nextColor;
        _nextColor = (_nextColor + 1) % _paletteSize;
        return color;
    }
}

public static class GitGraphPalette
{
    public const int Size = 8;
}
