# Design: Software on FPGA soft cores (`.fpgaproj` → RISC-V → `.swproj`)

Status: **Draft / proposal**. Nothing in here is implemented yet beyond what "Current state" lists.

## Goal

A user creates an FPGA project, adds a RISC-V soft core to it, then creates a software project (`.swproj`)
that targets that core. From the software project they can **Build**, **Program** (load the firmware) and
**Debug** (breakpoints, registers, memory) on the real hardware, without configuring compilers, linker
scripts, OpenOCD or GDB by hand.

## Current state (branch `feature/project-actions`)

### Project actions (`OneWare.Essentials/ProjectActions`)

- `ProjectActionKind`: `Build`, `Clean`, `Run`, `Program`, `Debug`.
- `IProjectActionProvider` (`Id`, `Priority`, `UsesSharedToolBar`, `Supports`, `ExecuteAsync`) and
  `IProjectActionService` (dispatch, busy state, cancel, nested actions such as Debug → Build).
- Results carry `BuildArtifact`s (`Executable`, `Elf`, `Hex`, `Binary`, `Bitstream`, `Other`) that later
  actions consume via `GetArtifacts(project)`.
- Shared `ProjectActionToolBarView` for all projects that don't bring their own toolbar.
- `FpgaProjectActionProvider`: Build → toolchain, Program → loader. Uses its own toolbar.

### Software projects (`OneWare.SoftwareProjectSystem`)

- `.swproj` (`SoftwareProjectRoot`): `buildSystem` (`simple`/`cmake`), `kit`, `configuration`, `output`,
  language standards, include dirs, defines, compiler/linker flags, libraries, CMake arguments.
- `ToolchainKitService` detects host gcc/clang and cross prefixes on PATH (`riscv-none-elf`,
  `riscv32-unknown-elf`, `riscv64-unknown-elf`, `riscv32-esp-elf`); plugins can `RegisterKit`.
- `SoftwareProjectActionProvider`: Build/Clean always; **Run/Debug only for host kits**; no Program.
  Debug launches GDB locally with no remote endpoint or target profile.

### Debugger (`OneWare.Debugger`, contracts in `OneWare.Essentials/Debugger`)

- Backend-neutral `IDebugSession` / `IDebugSessionLauncher`; GDB/MI implementation.
- `DebugLaunchRequest(BackendId, ExecutablePath, RemoteEndpoint, WorkingDirectory, InitCommands, TargetProfile)`.
  `RemoteEndpoint` is connected with `-target-select extended-remote`.
- `DebugTargetProfile`: addressable unit size, endianness, register list, read-only registers, breakpoint
  limit, call stack availability. Designed for soft cores (e.g. word-addressed memories).
- `IDebugTargetPreparer` (`CanPrepare`, `PrepareAsync` → `DebugLaunchRequest`, `CleanupAsync`): hook for
  plugins that bring a target up (configure FPGA, start OpenOCD / a GDB stub).
- `GdbLocator` picks `gdb-multiarch`, then `gdb`. No per-target GDB (e.g. `riscv-none-elf-gdb`).
- Breakpoints are scoped to the active project and to files GDB reports for the loaded program.

## Gaps

1. **No link between `.swproj` and the hardware.** A software project cannot reference the FPGA project or
   the soft core it runs on, so nothing provides ISA flags (`-march`/`-mabi`), linker script, startup code,
   memory map, debug transport or target profile.
2. **Cross kits only build.** `SoftwareProjectActionProvider.Supports` disables Run/Debug for non-host kits,
   and there is no Program action for `.swproj`.
3. **Program is undefined for firmware.** Possible ways to load software onto a soft core:
   - JTAG debug module: OpenOCD + GDB `load`.
   - UART/SPI bootloader in the core.
   - Initialise BRAM in the bitstream (needs `objcopy` → hex/mem and an FPGA rebuild or bitstream patching).
   No `objcopy` step exists yet to produce `Hex`/`Binary` artifacts.
4. **Debug ignores the target.** The `.swproj` Debug path does not use `IDebugTargetPreparer`,
   `RemoteEndpoint`, `TargetProfile`, or a target-matching GDB.
5. **FPGA Build returns no artifacts.** `FpgaProjectActionProvider` returns `Succeeded()` without the
   bitstream, so a software project cannot tell whether the hardware is built / up to date.

## Proposal

### 1. Platform contract in `OneWare.Essentials`

A *software platform* describes a programmable target that software projects can build for. Soft-core
plugins (or the FPGA project itself) provide it; the host machine is just another platform.

```csharp
public interface ISoftwarePlatform
{
    string Id { get; }                    // e.g. "fpga:soc.fpgaproj/cpu0"
    string DisplayName { get; }

    // Build
    string? PreferredKitTarget { get; }   // e.g. "riscv-none-elf"
    IReadOnlyList<string> CompilerFlags { get; }      // -march=rv32imc -mabi=ilp32 ...
    IReadOnlyList<string> LinkerFlags { get; }        // -T <linker script> -nostartfiles ...
    IReadOnlyList<string> IncludeDirectories { get; } // BSP headers (memory map, peripherals)
    IReadOnlyList<string> Sources { get; }            // startup code (crt0.S), BSP sources

    // Program / Debug
    bool CanProgram { get; }
    Task<ProjectActionResult> ProgramAsync(IProjectRoot software, IReadOnlyList<BuildArtifact> artifacts,
        CancellationToken token);
    IDebugTargetPreparer? CreateDebugPreparer(IProjectRoot software, BuildArtifact elf);
    DebugTargetProfile? TargetProfile { get; }
}

public interface ISoftwarePlatformService
{
    IEnumerable<ISoftwarePlatform> Platforms { get; }
    void RegisterPlatform(ISoftwarePlatform platform);   // or a provider that enumerates per project
    ISoftwarePlatform? GetPlatform(string id);
}
```

Platforms that depend on an FPGA project should be enumerated lazily by a provider (one per loaded
`.fpgaproj` × core instance), so they appear and disappear with the project tree.

Keep this additive (new interfaces only) so the `OneWare.Essentials` API stays plugin-compatible.

### 2. `.swproj` references a platform

New optional property:

```json
{
  "platform": "fpga:../soc/soc.fpgaproj/cpu0",
  "kit": "riscv-none-elf-gcc"
}
```

- Absent → host platform (current behaviour).
- The platform's flags, includes, startup sources and linker script are merged into the build in addition
  to the project's own settings (project settings append / override).
- Kit selection prefers kits whose `Target` matches `PreferredKitTarget`.
- Settings UI: a "Platform" combobox in the software project settings and creator dialog.

### 3. Actions for cross targets

`SoftwareProjectActionProvider.Supports`:

| Kind    | Host platform | Soft-core platform                     |
|---------|---------------|----------------------------------------|
| Build   | yes           | yes (+ `objcopy` → `Hex`/`Binary`)     |
| Clean   | yes           | yes                                    |
| Run     | yes           | no (or Program + reset)                |
| Program | no            | `platform.CanProgram`                  |
| Debug   | yes           | `platform.CreateDebugPreparer != null` |

- **Program**: build, then `platform.ProgramAsync(...)` with the ELF/Hex/Binary artifacts.
- **Debug**: build, then start through the platform's `IDebugTargetPreparer`. The preparer:
  1. makes sure the FPGA is configured (optionally runs the FPGA project's Program action),
  2. starts OpenOCD / the debug bridge,
  3. returns a `DebugLaunchRequest` with ELF, `RemoteEndpoint`, `InitCommands` (e.g. `load`,
     `monitor reset halt`) and `TargetProfile`.

### 4. GDB selection

Extend `GdbLocator` (or the launch request) so a target prefix can be passed: prefer `<prefix>-gdb`
(e.g. `riscv-none-elf-gdb`), then `gdb-multiarch`, then `gdb`. The kit's `Target` supplies the prefix.

### 5. FPGA side

- `FpgaProjectActionProvider.Build` should return the bitstream as a `Bitstream` artifact.
- A soft-core plugin (e.g. NEORV32 / VexRiscv / PicoRV32 integration) registers the platform provider: it
  knows the core instance in the FPGA project, its memory map, its debug module and its BSP.
- Optional "Build all" ordering: if the firmware is baked into BRAM, the software Build must run before the
  FPGA Build, and the FPGA project needs the software's `Hex` artifact as an input.

## Open questions

1. Where does the platform description live: generated from the FPGA project (core instances, memory map),
   or a descriptor file shipped by the core plugin?
2. Which load path(s) come first: JTAG/OpenOCD, UART bootloader, or BRAM init?
3. Should one `.swproj` target multiple platforms (host for unit tests + soft core)?
4. How are multi-project builds ordered (software → bitstream for BRAM init, or bitstream → software)?
5. Who ships the RISC-V toolchain, GDB and OpenOCD — package manager native tools, or PATH only?

## Suggested order of work

1. `ISoftwarePlatform` / service contracts + `platform` property in `.swproj` (host platform = today).
2. Platform-aware build (flags, linker script, startup code) + `objcopy` artifacts.
3. Program/Debug for cross kits through the platform (OpenOCD preparer), per-target GDB selection.
4. FPGA Build artifacts and a first soft-core platform plugin as reference implementation.
5. BRAM-init flow and cross-project build ordering.
