# Copilot instructions for OneWare

## Repository at a glance

- `OneWare.slnx` is the main solution entry point.
- `src/` contains the reusable modules and feature extensions.
- `studio/` contains the main applications:
  - `studio/OneWare.Studio.Desktop` for the desktop app
  - `studio/OneWare.Studio.Browser` for the browser app
  - `studio/OneWare.Studio` for shared studio code
- `tests/` contains the test projects.
- `docs/PluginDevelopment.md` is the best starting point for plugin architecture questions.
- `src/VtNetCore.Avalonia` is a required git submodule used by terminal-related projects.

## Environment and prerequisites

- Use .NET SDK `10.0.x`.
- `nuget.config` clears package sources and uses only `https://api.nuget.org/v3/index.json`.
- A repo-specific Copilot setup workflow already exists at `.github/workflows/copilot-setup-steps.yml`; it checks out submodules and installs .NET 10, but you should still run workload/package restore before building.

## First commands to run

From the repository root:

1. `git submodule update --init --recursive` if terminal-related code or submodule contents look incomplete.
2. `dotnet workload restore`
3. `dotnet restore`

Use the CI order from `.github/workflows/test.yml` for broad validation:

1. `dotnet workload restore`
2. `dotnet restore`
3. `dotnet build --no-restore`
4. `dotnet test --no-build --verbosity normal`

## Efficient validation strategy

- Prefer targeted validation when changes are local:
  - Build one project: `dotnet build src/<ProjectName>/<ProjectName>.csproj -v minimal`
  - Test one project: `dotnet test tests/<TestProject>/<TestProject>.csproj -v minimal`
- Use full-solution validation for cross-cutting changes, dependency changes, or anything touching shared infrastructure:
  - `dotnet build OneWare.slnx -v minimal`
  - `dotnet test OneWare.slnx -v minimal`
- For the desktop app entry point, run:
  - `dotnet run --project studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj`

## High-value code areas

- Core/shared infrastructure:
  - `src/OneWare.Essentials`
  - `src/OneWare.Core`
- AI/cloud-related modules:
  - `src/OneWare.Copilot`
  - `src/OneWare.Chat`
  - `src/OneWare.CloudIntegration`
- Common feature areas:
  - Package management: `src/OneWare.PackageManager`
  - Source control: `src/OneWare.SourceControl`
  - Terminal support: `src/OneWare.Terminal`, `src/OneWare.TerminalManager`, and the `src/VtNetCore.Avalonia` submodule
  - FPGA/project tooling: `src/OneWare.UniversalFpgaProjectSystem`, `src/OneWare.OssCadSuiteIntegration`, `src/OneWare.Verilog`, `src/OneWare.Vhdl`

## Working conventions

- Make small, targeted changes and avoid unrelated cleanup.
- Keep changes compatible with .NET 10 and each project's existing nullable settings.
- When changing package manager behavior, inspect both:
  - `src/OneWare.PackageManager/Services/PackageService.cs`
  - `src/OneWare.PackageManager/ViewModels/PackageManagerViewModel.cs`
- For offline or network-sensitive paths, prefer graceful fallback and logging over surfacing raw exceptions to users.
- Treat scripts like `cleanall.sh`, `cleanall.ps1`, and packaging helpers under `build/` and `snap/` as potentially disruptive; do not run them unless the task requires them.

## Known issues and workarounds

- **Missing submodule contents**
  - Symptom: terminal-related projects fail because `src/VtNetCore.Avalonia` content is missing.
  - Workaround: run `git submodule update --init --recursive`.

- **Missing .NET workloads**
  - Symptom: restore/build failures after checkout, especially around Avalonia/browser tooling.
  - Workaround: run `dotnet workload restore` before `dotnet restore` or `dotnet build`.

- **Avalonia build task log permission error in restricted sandboxes**
  - Symptom: build fails with `MSB4018`, `UnauthorizedAccessException`, or `Permission denied` while writing `~/.local/share/AvaloniaUI/BuildServices/buildtasks.log`.
  - Workaround: rerun the same `dotnet build` command in a session/runner with permission to write that path. This is a known sandbox restriction for this repository.

## Errors encountered during onboarding

- No additional repository-specific errors were encountered while preparing these instructions beyond the known sandbox/build restrictions documented above.
