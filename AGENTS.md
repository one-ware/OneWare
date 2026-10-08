# AGENTS.md

This file provides repo-specific guidance for coding agents working in `OneWare`.

## Repository Overview

- Solution entry: `OneWare.slnx`
- Main source tree: `src/`
- Tests: `tests/`
- Docs: `docs/` (see [Key Docs](#key-docs))
- Build props and packaging helpers: `build/props/`, `snap/`, `cleanall.sh`, `buildsnap.sh`
- Studio app projects:
  - Desktop: `studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj`
  - Browser (WASM): `studio/OneWare.Studio.Browser/OneWare.Studio.Browser.csproj`
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
- `.github/workflows/copilot-setup-steps.yml` prepares the Copilot cloud agent environment the same way.

## Build And Validation

- Preferred targeted build:
  - `dotnet build src/<ProjectName>/<ProjectName>.csproj -v minimal`
- Full solution build:
  - `dotnet build OneWare.slnx -v minimal`
- Run Studio desktop from repo root:
  - `dotnet run --project studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj`
- Targeted tests:
  - `dotnet test tests/<TestProject>/<TestProject>.csproj -v minimal`
  - Single class or test: add `--filter "FullyQualifiedName~<ClassOrTestName>"`
- Full tests:
  - `dotnet test OneWare.slnx -v minimal`
- XAML errors (bad selectors, unknown properties, missing `x:DataType` for compiled bindings) only surface at
  build time, so build the project that owns any changed `.axaml` file.

## Tests

- Framework: xUnit v2 on VSTest (`build/props/XUnit.props`), so use `--filter`, not MTP-style flags.
- Naming: `tests/OneWare.<Module>.UnitTests` (logic) and `*.Tests` / `*.HeadlessTests` (UI).
- Headless Avalonia UI tests import `build/props/Avalonia.Headless.XUnit.props`, declare
  `[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]` in `TestApp.cs`, and use `[AvaloniaFact]`
  (see `tests/OneWare.Dock.HeadlessTests` and `tests/OneWare.PackageManager.UnitTests`).
- `tests/OneWare.TestPlugin` is a minimal plugin used by tests, not a test project.
- New test projects must be added to the `/Tests/` folder in `OneWare.slnx`, otherwise CI won't run them.

## Known Build Restriction In Sandboxed Sessions

- Avalonia build tasks attempt to write telemetry logs under:
  - `~/.local/share/AvaloniaUI/BuildServices/buildtasks.log`
- In restricted/sandboxed environments, this path may be blocked and cause build failure with `MSB4018` (`UnauthorizedAccessException` / `Permission denied`).
- If that happens, rerun the same `dotnet build` command with elevated permissions for the session.
- On `linux-x64` publish, `OneWare.Terminal.dll` is intentionally excluded from ReadyToRun (`<PublishReadyToRunExclude>`) to avoid a known crash in `SetWindowSize`.

## Architecture Map

- UI stack: **Avalonia 11.3** (`build/props/Avalonia.props`) with `SimpleTheme`, Dock.Avalonia and AvaloniaEdit.
  Don't use Avalonia 12 APIs or samples. MVVM uses CommunityToolkit.Mvvm (`ObservableObject`, `SetProperty`,
  `RelayCommand`); ReactiveUI is only used in a few places.
- Core/shared infrastructure:
  - `src/OneWare.Essentials` — base types, services interfaces, shared controls, the design system (`Styles/`), Avalonia/ReactiveUI wiring, ONNX/OpenCV/AI abstractions. Published as a NuGet package that external plugins compile against, so keep its public API backwards compatible.
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
- Module registration happens in three layers (`moduleCatalog.AddModule<T>()`):
  - `src/OneWare.Core/App.axaml.cs` — built-in panels shipped in every app (Studio, Browser, Demo)
  - `studio/OneWare.Studio/StudioApp.cs` — Studio modules for desktop and browser
  - `studio/OneWare.Studio.Desktop/DesktopStudioApp.cs` — desktop-only modules (processes, native tools, terminals)
- Plugin development reference:
  - `docs/PluginDevelopment.md`
  - `demo/OneWare.Demo` — minimal runnable demo app showing plugin patterns

## Key Docs

- `CONTRIBUTING.md` — contribution workflow (branching, PRs, tests) for humans and agents.
- `docs/README.md` — developer wiki: architecture, app layers, CLI options/env vars, log and data locations.
- `docs/Styling.md` — design tokens, style classes, icons and styling rules. **Read before editing any `.axaml`.**
- `docs/PluginDevelopment.md` — module lifecycle, `OneWare.Essentials` service reference, FPGA project system, AI agents/skills inside OneWare.
- `docs/SourceControl.md` — source control module notes.
- `docs/changelog.md` — user-facing release notes (newest first).

## Agent Skills

Repo-specific skills live in `.agents/skills/` (picked up by GitHub Copilot and other agents that support the
Agent Skills standard). Load the matching skill before starting such a task:

- `oneware-styling` — any `.axaml` view/style work, adding style classes, tokens or icons.
- `oneware-module` — creating or wiring a module/plugin project, DI registration, studio registration.
- `oneware-release` — version bumps, metainfo release entries and changelog.

Keep these skills in sync when the conventions they describe change (for example when `docs/Styling.md` or the
module registration layout changes).

## Working Conventions

- Do not revert unrelated local changes; the workspace may be intentionally dirty.
- Prefer small, targeted fixes and project-level builds relevant to changed files.
- When changing package manager behavior, verify:
  - `src/OneWare.PackageManager/Services/PackageService.cs`
  - `src/OneWare.PackageManager/ViewModels/PackageManagerViewModel.cs`
- For offline/network-sensitive flows, avoid surfacing user-facing exceptions; prefer graceful fallback and logging.
- Keep changes compatible with .NET 10 and existing nullable settings in each project.
- **Package versions are centralized** in `build/props/*.props` (one file per package). Import the relevant `.props` file in a project's `.csproj` rather than adding inline `<PackageReference>` with a version. The global ONNX Runtime version lives in `Directory.Build.props` (`OnnxRuntimeVersion`).
- **Module entry point**: every feature module exposes a `*Module.cs` deriving from `OneWareModuleBase` (e.g., `CopilotModule.cs`, `ChatModule.cs`, `OneWareCloudIntegrationModule.cs`) that wires the module into the DI container. New modules must follow this pattern.
- All module `.csproj` files import `build/props/Base.props` (version and package metadata, `StudioVersion`) and `build/props/OneWare.Module.props` (`TargetFramework`, `Nullable`, `ImplicitUsings`, `LangVersion`, `AvaloniaUseCompiledBindingsByDefault`).
- **UI styling**: use the shared classes and `{DynamicResource}` tokens from `OneWare.Essentials/Styles` instead of inline colors, borders, font sizes or local generic styles. When you add or change a shared class, update `StyleGallery.axaml` and `docs/Styling.md` too.
- Browser (WASM) builds share `studio/OneWare.Studio`; code there must not start processes or rely on native libraries.

## Scripts And Safety

- Treat these scripts as potentially disruptive; do not run them unless explicitly required:
  - `cleanall.sh` / `cleanall.ps1` (delete build output across the repo)
  - `buildsnap.sh`, `trysnap.sh` (snap packaging/install)
  - `flatpak-dotnet-generator.py` (regenerates Flatpak NuGet sources)
- Don't bump `StudioVersion` or edit release metadata unless asked (see the `oneware-release` skill).

## Tooling Notes

- `rg` may not be installed in some environments. Use `find` + `grep` as fallback.
- `nuget.config` clears sources and uses only `https://api.nuget.org/v3/index.json` by default.
