# AGENTS.md

This file provides repo-specific guidance for coding agents working in `OneWare`.

## Repository Overview

- Solution entry: `OneWare.slnx`
- Main source tree: `src/`
- Tests: `tests/`
- Build scripts and packaging helpers: `build/`, `snap/`, `cleanall.sh`, `buildsnap.sh`
- Studio app projects:
  - Desktop: `studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj`
  - Browser: `studio/OneWare.Studio.Browser/OneWare.Studio.Browser.csproj`
  - Shared studio: `studio/OneWare.Studio/OneWare.Studio.csproj`
- Submodule in use:
  - `src/VtNetCore.Avalonia` (required for terminal-related projects)

## Prerequisites

- .NET SDK `10.0.x`
- Clone with submodules:
  - `git clone --recursive <repo-url>`
  - or `git submodule update --init --recursive` in an existing clone

## Bootstrap Commands (CI-aligned)

- CI (`.github/workflows/test.yml`) runs these steps:
  - `dotnet workload restore`
  - `dotnet restore`
  - `dotnet build --no-restore`
  - `dotnet test --no-build --verbosity normal`
- For local agent validation, prefer the same order when touching multiple projects.

## Build And Validation

- Preferred targeted build:
  - `dotnet build src/<ProjectName>/<ProjectName>.csproj -v minimal`
- Full solution build:
  - `dotnet build OneWare.slnx -v minimal`
- Run Studio desktop from repo root:
  - `dotnet run --project studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj`
- Targeted tests:
  - `dotnet test tests/<TestProject>/<TestProject>.csproj -v minimal`
- Full tests:
  - `dotnet test OneWare.slnx -v minimal`

## Known Build Restriction In Sandboxed Sessions

- Avalonia build tasks attempt to write telemetry logs under:
  - `~/.local/share/AvaloniaUI/BuildServices/buildtasks.log`
- In restricted/sandboxed environments, this path may be blocked and cause build failure with `MSB4018` (`UnauthorizedAccessException` / `Permission denied`).
- If that happens, rerun the same `dotnet build` command with elevated permissions for the session.
- On `linux-x64` publish, `OneWare.Terminal.dll` is intentionally excluded from ReadyToRun (`<PublishReadyToRunExclude>`) to avoid a known crash in `SetWindowSize`.

## Architecture Map

- Core/shared infrastructure:
  - `src/OneWare.Essentials` — base types, services interfaces, Avalonia/ReactiveUI wiring, ONNX/OpenCV/AI abstractions
  - `src/OneWare.Core` — app shell (`App.axaml`), Serilog logging, DI bootstrap; depends on `OneWare.ApplicationCommands` and all built-in UI panels
  - `src/OneWare.ApplicationCommands` — shared application command definitions consumed by `OneWare.Core`
- AI / cloud integrations:
  - `src/OneWare.CloudIntegration` — SignalR + JWT auth backend connectivity (uses `RestSharp`, `Devlooped.CredentialManager`)
  - `src/OneWare.Chat` — chat UI panel
  - `src/OneWare.Copilot` — GitHub Copilot integration (`GitHub.Copilot.SDK`; sets `CopilotSkipCliDownload=true`)
- Feature modules/extensions (examples):
  - Package manager: `src/OneWare.PackageManager`
  - Source control: `src/OneWare.SourceControl`
  - FPGA project system: `src/OneWare.UniversalFpgaProjectSystem`
  - Tool/language integrations: `src/OneWare.*` projects under `src/`
  - `src/OneWare.CSharp` is present but **commented out** in the Desktop studio project
- Plugin development reference:
  - `docs/PluginDevelopment.md`
  - `demo/OneWare.Demo` — minimal runnable demo app showing plugin patterns

## Working Conventions

- Do not revert unrelated local changes; the workspace may be intentionally dirty.
- Prefer small, targeted fixes and project-level builds relevant to changed files.
- When changing package manager behavior, verify:
  - `src/OneWare.PackageManager/Services/PackageService.cs`
  - `src/OneWare.PackageManager/ViewModels/PackageManagerViewModel.cs`
- For offline/network-sensitive flows, avoid surfacing user-facing exceptions; prefer graceful fallback and logging.
- Keep changes compatible with .NET 10 and existing nullable settings in each project.
- **Package versions are centralized** in `build/props/*.props` (one file per package). Import the relevant `.props` file in a project's `.csproj` rather than adding inline `<PackageReference>` with a version. The global ONNX Runtime version lives in `Directory.Build.props` (`OnnxRuntimeVersion`).
- **Module entry point**: every feature module exposes a `*Module.cs` (e.g., `CopilotModule.cs`, `ChatModule.cs`, `OneWareCloudIntegrationModule.cs`) that wires the module into the DI container. New modules must follow this pattern.
- All module `.csproj` files import `build/props/Base.props` and `build/props/OneWare.Module.props`; these set `TargetFramework`, `Nullable`, `LangVersion`, and `AvaloniaUseCompiledBindingsByDefault`.

## Scripts And Safety

- Treat these scripts as potentially disruptive; do not run them unless explicitly required.

## Tooling Notes

- `rg` may not be installed in some environments. Use `find` + `grep` as fallback.
- `nuget.config` clears sources and uses only `https://api.nuget.org/v3/index.json` by default.
