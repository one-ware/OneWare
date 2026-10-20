using OneWare.Essentials.Models;
using OneWare.Essentials.ProjectActions;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

/// <summary>
/// Maps the shared project actions onto FPGA projects: Build runs the toolchain, Program runs the loader.
/// </summary>
public class FpgaProjectActionProvider(FpgaService fpgaService) : IProjectActionProvider
{
    public const string ProviderId = "fpga";

    public string Id => ProviderId;

    // FPGA projects keep their own toolbar (Compile / Config FPGA with plugin menu extensions).
    public bool UsesSharedToolBar => false;

    public bool Supports(IProjectRoot project, ProjectActionKind kind)
    {
        if (project is not UniversalFpgaProjectRoot fpgaProject) return false;

        return kind switch
        {
            ProjectActionKind.Build => fpgaProject.Toolchain != null,
            ProjectActionKind.Program => fpgaProject.Loader != null,
            _ => false
        };
    }

    public async Task<ProjectActionResult> ExecuteAsync(ProjectActionRequest request)
    {
        if (request.Project is not UniversalFpgaProjectRoot project)
            return ProjectActionResult.Failed("Not an FPGA project.");

        switch (request.Kind)
        {
            case ProjectActionKind.Build:
                return await fpgaService.RunToolchainAsync(project)
                    ? ProjectActionResult.Succeeded()
                    : ProjectActionResult.Failed("Compilation failed.");

            case ProjectActionKind.Program:
                var loader = fpgaService.Loaders.FirstOrDefault(x => x.Id == project.Loader);
                if (loader == null) return ProjectActionResult.Failed($"Loader '{project.Loader}' is not installed.");

                await loader.DownloadAsync(project);
                return ProjectActionResult.Succeeded();

            default:
                return ProjectActionResult.Failed($"{request.Kind} is not supported for FPGA projects.");
        }
    }
}
