namespace OneWare.Essentials.ProjectActions;

/// <summary>
/// The project-type independent actions the IDE offers for the active project.
/// What an action means is decided by the <see cref="IProjectActionProvider"/> of the project type,
/// e.g. Build compiles a C++ program or synthesizes an FPGA design, Program flashes a microcontroller or
/// configures an FPGA.
/// </summary>
public enum ProjectActionKind
{
    Build,
    Clean,
    Run,
    Program,
    Debug
}
