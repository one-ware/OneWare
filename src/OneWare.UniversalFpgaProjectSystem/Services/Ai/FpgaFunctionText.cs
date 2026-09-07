using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OneWare.UniversalFpgaProjectSystem.Services.Ai;

/// <summary>
/// Formatting helpers shared by the FPGA chat functions, so every reply uses the same shape.
/// </summary>
public static class FpgaFunctionText
{
    /// <summary>Appends a <c>Name: value</c> line, using a placeholder for empty values.</summary>
    public static void AppendField(StringBuilder builder, string name, object? value)
    {
        var text = value?.ToString();

        builder.AppendLine($"{name}: {(string.IsNullOrWhiteSpace(text) ? "(unset)" : text)}");
    }

    /// <summary>Appends a bullet list, or a placeholder when the list is empty.</summary>
    public static void AppendList(StringBuilder builder, string title, IEnumerable<string> items,
        string emptyText = "(none)")
    {
        var list = items.ToList();

        builder.AppendLine($"{title}:");

        if (list.Count == 0)
        {
            builder.AppendLine($"  {emptyText}");
            return;
        }

        foreach (var item in list) builder.AppendLine($"  - {item}");
    }

    /// <summary>Parses a boolean argument, accepting the spellings a model is likely to produce.</summary>
    /// <exception cref="FpgaAgentException">The value is not a boolean.</exception>
    public static bool ParseBool(string value, string argumentName)
    {
        var trimmed = value.Trim();

        if (bool.TryParse(trimmed, out var parsed)) return parsed;

        return trimmed.ToLowerInvariant() switch
        {
            "1" or "yes" or "y" or "on" or "enabled" => true,
            "0" or "no" or "n" or "off" or "disabled" => false,
            _ => throw new FpgaAgentException(
                $"'{value}' is not a boolean value for '{argumentName}'. Use true or false.")
        };
    }

    /// <summary>Throws when a required text argument is missing.</summary>
    /// <exception cref="FpgaAgentException">The argument is empty.</exception>
    public static string Require(string? value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FpgaAgentException($"The argument '{argumentName}' is required.");

        return value.Trim();
    }
}

/// <summary>
/// Builds the short detail string the chat shows next to a running FPGA tool call.
/// </summary>
/// <remarks>
/// While a function runs the chat only shows its friendly name, which makes a transcript of several
/// <c>fpga_set_device_setting</c> calls unreadable. Each function therefore contributes a detail
/// naming what it acts on, derived from the actual invocation arguments.
/// </remarks>
public static class FpgaFunctionDetail
{
    /// <summary>Details longer than this are cut, so a long argument cannot break the layout.</summary>
    private const int MaxLength = 80;

    /// <summary>Builds a detail that does not depend on the arguments.</summary>
    public static Func<AIFunctionArguments, string?> Constant(string detail)
    {
        return _ => detail;
    }

    /// <summary>Builds a detail from the first argument that has a value.</summary>
    public static Func<AIFunctionArguments, string?> FirstOf(string fallback, params string[] argumentNames)
    {
        ArgumentNullException.ThrowIfNull(argumentNames);

        return arguments =>
        {
            foreach (var name in argumentNames)
                if (GetString(arguments, name) is { } value)
                    return Shorten(value);

            return fallback;
        };
    }

    /// <summary>Builds a detail that joins every argument that has a value.</summary>
    public static Func<AIFunctionArguments, string?> AllOf(string fallback, params string[] argumentNames)
    {
        ArgumentNullException.ThrowIfNull(argumentNames);

        return arguments =>
        {
            var values = new List<string>(argumentNames.Length);

            foreach (var name in argumentNames)
                if (GetString(arguments, name) is { } value)
                    values.Add(value.Trim());

            return values.Count == 0 ? fallback : Shorten(string.Join(' ', values));
        };
    }

    /// <summary>
    /// Reads an invocation argument as text.
    /// </summary>
    /// <remarks>
    /// Depending on how the model call was deserialized a value arrives either as a CLR type or as a
    /// <see cref="JsonElement"/>, so both shapes are handled here.
    /// </remarks>
    private static string? GetString(AIFunctionArguments? arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var raw) || raw is null)
            return null;

        var text = raw switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString(),
            JsonElement json => json.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => raw.ToString()
        };

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>Cuts a detail to the display length.</summary>
    private static string Shorten(string detail)
    {
        var single = detail.ReplaceLineEndings(" ").Trim();

        return single.Length <= MaxLength ? single : string.Concat(single.AsSpan(0, MaxLength - 1), "…");
    }
}

/// <summary>
/// Resolves the directory the FPGA agent skills are deployed to.
/// </summary>
/// <remarks>
/// The skills are copied next to the assembly by the build, so resolving them depends on the
/// assembly having a physical location. That is not guaranteed for every deployment model, so the
/// lookup fails softly instead of tearing down module initialisation.
/// </remarks>
public static class FpgaSkillDirectoryLocator
{
    /// <summary>Name of the folder the skills are deployed to, relative to the assembly.</summary>
    public const string SkillFolderName = "Skills";

    /// <summary>Resolves the skill directory that belongs to <paramref name="assembly"/>.</summary>
    public static string? TryResolve(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return TryResolve(assembly.Location);
    }

    /// <summary>Resolves the skill directory next to <paramref name="assemblyLocation"/>.</summary>
    public static string? TryResolve(string? assemblyLocation)
    {
        if (string.IsNullOrEmpty(assemblyLocation)) return null;

        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

        if (string.IsNullOrEmpty(assemblyDirectory)) return null;

        var skillDirectory = Path.Combine(assemblyDirectory, SkillFolderName);

        return Directory.Exists(skillDirectory) ? skillDirectory : null;
    }
}
