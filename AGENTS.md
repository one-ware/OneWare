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

## Architecture Map

- Core/shared infrastructure:
  - `src/OneWare.Essentials`
  - `src/OneWare.Core`
- Feature modules/extensions (examples):
  - Package manager: `src/OneWare.PackageManager`
  - Source control: `src/OneWare.SourceControl`
  - Tool/language integrations: `src/OneWare.*` projects under `src/`
- Plugin development reference:
  - `docs/PluginDevelopment.md`

## Working Conventions

- Do not revert unrelated local changes; the workspace may be intentionally dirty.
- Prefer small, targeted fixes and project-level builds relevant to changed files.
- When changing package manager behavior, verify:
  - `src/OneWare.PackageManager/Services/PackageService.cs`
  - `src/OneWare.PackageManager/ViewModels/PackageManagerViewModel.cs`
- For offline/network-sensitive flows, avoid surfacing user-facing exceptions; prefer graceful fallback and logging.
- Keep changes compatible with .NET 10 and existing nullable settings in each project.

## Scripts And Safety

- Treat these scripts as potentially disruptive; do not run them unless explicitly required.

## Tooling Notes

- `rg` may not be installed in some environments. Use `find` + `grep` as fallback.
- `nuget.config` clears sources and uses only `https://api.nuget.org/v3/index.json` by default.
