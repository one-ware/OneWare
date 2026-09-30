# Contributing to OneWare Studio

Thanks for your interest in improving OneWare Studio! This guide covers how to report issues, set up a
development environment and get a pull request merged.

- Questions and discussion: [Discord](https://discord.gg/NCN9VAh)
- Developer documentation: [docs/](docs/README.md)
- Security vulnerabilities: **don't open a public issue**. Follow [SECURITY.md](SECURITY.md).

## Reporting bugs and requesting features

1. Search the [open issues](https://github.com/one-ware/OneWare/issues) first to avoid duplicates.
2. For bugs, include:
   - OneWare Studio version (Help → About) and how you installed it (installer, Snap, Flathub, WinGet, source).
   - Operating system and architecture.
   - Steps to reproduce, expected and actual behavior.
   - The log file of the session, found in `~/OneWareStudio/Logs` (or the folder set with `--oneware-dir`).
3. For feature requests, describe the problem you want to solve, not only the solution.

Issues labelled [`help wanted`](https://github.com/one-ware/OneWare/labels/help%20wanted) are a good place
to start. Comment on an issue before starting larger work so we can agree on the approach.

## Development setup

Requirements:

- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- Git, and an IDE with Avalonia support (JetBrains Rider or Visual Studio / VS Code with the Avalonia extension)

```bash
git clone --recursive https://github.com/one-ware/OneWare.git
cd OneWare
dotnet workload restore
dotnet build OneWare.slnx
dotnet run --project studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj
```

The repository uses a git submodule (`src/VtNetCore.Avalonia`). If you cloned without `--recursive`, run
`git submodule update --init --recursive`.

See the [developer documentation](docs/README.md) for the project structure, architecture and debugging tips.

## Making changes

- Branch from `main` and use a descriptive branch name, for example `fix/package-status-prerelease` or
  `feature/copilot-sub-agents`.
- Keep pull requests focused. Unrelated refactorings and formatting changes make reviews harder.
- Follow the existing code style of the file you're editing. The solution enables nullable reference types,
  so don't add new nullable warnings.
- **Dependencies**: package versions are centralized in `build/props/<Package>.props`. Import the `.props` file
  in your `.csproj` instead of adding a `PackageReference` with a version.
- **UI**: use the shared style classes and `{DynamicResource}` tokens from `OneWare.Essentials`, not inline
  colors, borders or font sizes. See [docs/Styling.md](docs/Styling.md). If you add a shared style class, add
  it to the Style Gallery and the styling doc.
- **Public API**: `OneWare.Essentials`, `OneWare.UniversalFpgaProjectSystem` and the other packages listed in the
  [README](README.md#nuget) are consumed by external plugins. Avoid breaking changes to their public types; if
  one is unavoidable, call it out in the pull request.
- **Docs**: update the relevant file in `docs/` when you change behavior that it describes, and add
  user-facing changes to `docs/changelog.md` only if a maintainer asks you to (the changelog is written
  at release time).
- Don't bump `StudioVersion` or edit the release metadata; maintainers do that when releasing.

## Tests

Tests live in `tests/` and use xUnit. UI tests run headless with `Avalonia.Headless.XUnit`.

```bash
dotnet test OneWare.slnx                                                   # everything (same as CI)
dotnet test tests/OneWare.Vhdl.UnitTests/OneWare.Vhdl.UnitTests.csproj     # one project
dotnet test tests/OneWare.Vhdl.UnitTests --filter "FullyQualifiedName~MyTest"  # one test
```

Add or update tests for bug fixes and new logic where practical. New test projects must be added to the
`/Tests/` folder in `OneWare.slnx`, otherwise CI won't run them.

## Pull requests

1. Make sure `dotnet build OneWare.slnx` and `dotnet test OneWare.slnx` pass locally.
2. Open the pull request against `main` and describe **what** changed and **why**. Link the issue it fixes
   (`Fixes #123`).
3. Add screenshots or a short recording for UI changes, ideally in both the Light and Dark theme.
4. CI runs the build, all tests and a dependency vulnerability scan. Please keep it green.
5. A maintainer will review the pull request. Address feedback with new commits; we squash or merge when it's ready.

## AI-assisted contributions

AI coding agents are welcome. The repository ships [AGENTS.md](AGENTS.md) and repo-specific skills in
`.agents/skills/` so agents follow the same conventions. You are responsible for reviewing, building and
testing everything you submit.

## License

By contributing, you agree that your contributions are licensed under the [Apache License 2.0](LICENSE).
