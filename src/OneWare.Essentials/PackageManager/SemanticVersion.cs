using System.Globalization;

namespace OneWare.Essentials.PackageManager;

/// <summary>
///     Semantic version of a package, e.g. <c>7.0.0-dev.20260707.2</c>.
///     <see cref="System.Version" /> cannot parse prerelease or build metadata suffixes, which would make
///     installed prerelease packages look like they were never installed.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private SemanticVersion(int[] numbers, string[] prereleaseIdentifiers)
    {
        _numbers = numbers;
        _prereleaseIdentifiers = prereleaseIdentifiers;
    }

    private readonly int[] _numbers;
    private readonly string[] _prereleaseIdentifiers;

    public bool IsPrerelease => _prereleaseIdentifiers.Length > 0;

    /// <summary>
    ///     Parses a version like <c>1.2</c>, <c>1.2.3.4</c>, <c>1.2.3-beta.1</c> or <c>1.2.3-beta+build.5</c>.
    ///     Build metadata is ignored, as required by semver.
    /// </summary>
    public static bool TryParse(string? version, out SemanticVersion result)
    {
        result = Empty;

        if (string.IsNullOrWhiteSpace(version)) return false;

        var span = version.Trim();

        if (span.StartsWith('v') || span.StartsWith('V')) span = span[1..];

        var buildIndex = span.IndexOf('+');
        if (buildIndex >= 0) span = span[..buildIndex];

        var prereleaseIndex = span.IndexOf('-');
        var prerelease = prereleaseIndex >= 0 ? span[(prereleaseIndex + 1)..] : string.Empty;
        if (prereleaseIndex >= 0)
        {
            if (prerelease.Length == 0) return false;
            span = span[..prereleaseIndex];
        }

        if (span.Length == 0) return false;

        var parts = span.Split('.');
        var numbers = new int[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
                return false;

            numbers[i] = number;
        }

        var identifiers = prerelease.Length == 0
            ? []
            : prerelease.Split('.');

        if (identifiers.Any(string.IsNullOrEmpty)) return false;

        result = new SemanticVersion(numbers, identifiers);
        return true;
    }

    public static readonly SemanticVersion Empty = new([], []);

    public int CompareTo(SemanticVersion? other)
    {
        if (other == null) return 1;

        var length = Math.Max(_numbers.Length, other._numbers.Length);
        for (var i = 0; i < length; i++)
        {
            var mine = i < _numbers.Length ? _numbers[i] : 0;
            var theirs = i < other._numbers.Length ? other._numbers[i] : 0;

            if (mine != theirs) return mine.CompareTo(theirs);
        }

        // A version without a prerelease suffix outranks the prereleases leading up to it.
        if (_prereleaseIdentifiers.Length == 0 && other._prereleaseIdentifiers.Length == 0) return 0;
        if (_prereleaseIdentifiers.Length == 0) return 1;
        if (other._prereleaseIdentifiers.Length == 0) return -1;

        var identifierCount = Math.Min(_prereleaseIdentifiers.Length, other._prereleaseIdentifiers.Length);
        for (var i = 0; i < identifierCount; i++)
        {
            var comparison = CompareIdentifier(_prereleaseIdentifiers[i], other._prereleaseIdentifiers[i]);
            if (comparison != 0) return comparison;
        }

        return _prereleaseIdentifiers.Length.CompareTo(other._prereleaseIdentifiers.Length);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightIsNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftIsNumeric && rightIsNumeric) return leftNumber.CompareTo(rightNumber);
        if (leftIsNumeric) return -1;
        if (rightIsNumeric) return 1;

        return string.CompareOrdinal(left, right);
    }

    public bool Equals(SemanticVersion? other)
    {
        return CompareTo(other) == 0;
    }

    public override bool Equals(object? obj)
    {
        return obj is SemanticVersion other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var number in _numbers) hash.Add(number);
        foreach (var identifier in _prereleaseIdentifiers) hash.Add(identifier);
        return hash.ToHashCode();
    }

    public static bool operator >(SemanticVersion? left, SemanticVersion? right)
    {
        return Comparer<SemanticVersion>.Default.Compare(left, right) > 0;
    }

    public static bool operator <(SemanticVersion? left, SemanticVersion? right)
    {
        return Comparer<SemanticVersion>.Default.Compare(left, right) < 0;
    }

    public static bool operator >=(SemanticVersion? left, SemanticVersion? right)
    {
        return Comparer<SemanticVersion>.Default.Compare(left, right) >= 0;
    }

    public static bool operator <=(SemanticVersion? left, SemanticVersion? right)
    {
        return Comparer<SemanticVersion>.Default.Compare(left, right) <= 0;
    }

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right)
    {
        return Comparer<SemanticVersion>.Default.Compare(left, right) == 0;
    }

    public static bool operator !=(SemanticVersion? left, SemanticVersion? right)
    {
        return !(left == right);
    }
}
