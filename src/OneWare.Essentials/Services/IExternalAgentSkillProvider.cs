namespace OneWare.Essentials.Services;

/// <summary>
/// Provides skills for external coding agents independently of the Studio chat.
/// </summary>
/// <remarks>
/// Implement this on a public, non-abstract plugin class with a public parameterless constructor.
/// Each returned discovery root contains skill directories, each with a SKILL.md and optional
/// supporting files. The directories must exist in the installed plugin.
/// </remarks>
public interface IExternalAgentSkillProvider
{
    IReadOnlyCollection<string> GetSkillDirectories();
}
