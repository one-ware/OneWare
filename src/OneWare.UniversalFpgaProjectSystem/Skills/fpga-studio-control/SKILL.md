---
name: fpga-studio-control
description: How to drive ONE WARE Studio's FPGA project system from chat — which fpga_* function to call for creating a project, selecting board/toolchain/loader, setting the top entity, compiling, and running test benches, and in what order.
---

# Controlling the FPGA project system

Every `fpga_*` function acts on the **active** FPGA project. There is exactly one active project
at a time, and it is not necessarily the project whose file the user has open in the editor.

## Orientation

| Question | Function |
| --- | --- |
| Is an FPGA project active, and how is it configured? | `fpga_get_project_overview` |
| Which projects are loaded? | `fpga_list_projects` |
| What is installed (toolchains, loaders, simulators, boards, templates, pre-compile steps)? | `fpga_list_toolchains` |
| Which files belong to the project, and how are they flagged? | `fpga_list_files` |

Start every task with `fpga_get_project_overview`. If it reports that no project is active, either
`fpga_set_active_project` an existing one, `fpga_open_project` a `.fpgaproj` from disk, or create a
new one.

`fpga_list_toolchains` is the only authoritative source of ids. A toolchain, loader, simulator or
board that it does not list is not installed, and no other function will accept it. Never guess an
id from the user's wording — map "iCE40 board" or "Yosys" onto a listed entry, and if nothing
matches, say what is available.

## Creating a project

```
fpga_list_toolchains            -> pick a toolchain id and a loader id
fpga_create_project             -> name, optional directory, toolchain, loader, template
fpga_set_board                  -> a board name from fpga_list_toolchains
```

`fpga_create_project` creates a folder, writes the `.fpgaproj`, loads the project and makes it
active. It asks the user to confirm because it writes to disk. It does **not** select a board — a
board is required before compiling, so always follow up with `fpga_set_board`.

The optional template fills the project with starter sources. If the user wants to write the HDL
themselves, omit it.

## Configuring a project

| Task | Function |
| --- | --- |
| Change the toolchain | `fpga_set_toolchain` |
| Change the loader (programmer) | `fpga_set_loader` |
| Change the board | `fpga_set_board` |
| Set the top level entity/module | `fpga_set_top_entity` |
| Change a registered project setting | `fpga_set_project_setting` |
| Change a board device setting | `fpga_set_device_setting` |
| Exclude a file from compilation | `fpga_set_compile_excluded` |
| Mark a file as a test bench | `fpga_set_testbench` |

All of these save the project file, so no separate save is needed. `fpga_save_project` exists only
for the case where the project file was edited by other means.

### Top entity

`fpga_list_top_entities` parses the HDL sources and returns every entity/module that could be the
top level, together with the file it lives in. Test benches and files excluded from compilation are
skipped, which is exactly what you want — a test bench is never the top entity of a synthesis run.

Only set a top entity that the list contains. If the list is empty, the language plugin for the
sources is probably not installed; say so instead of setting a name that will fail at synthesis.

### Project settings vs. device settings

Two different stores, two different functions:

- **Project settings** (`fpga_list_project_settings` / `fpga_set_project_setting`) are what the
  project settings dialog shows. They live in the `.fpgaproj` file and are registered by plugins,
  so which ones exist depends on the selected toolchain. Keys of the form `preCompileStep_<id>`
  are booleans that enable or disable a pre-compile step.
- **Device settings** (`fpga_list_device_settings` / `fpga_set_device_setting`) describe the
  selected board and live in `device-settings/<board>.deviceconf`. They only exist once a board is
  selected. Setting one to an empty value removes it, so the board default applies again.

### Compile exclusions

A file that is excluded from compilation stays in the project and stays visible, but is not passed
to synthesis. Use it for alternative implementations of the same entity, for vendor sources that
only the simulator needs, and for anything the user wants to keep but not build.

Excluding a file is the correct answer to "the synthesizer complains about a file I don't need" —
deleting it is not.

## Compiling

```
fpga_get_project_overview       -> toolchain, board and top entity present?
fpga_compile
```

`fpga_compile` saves the open files, runs the enabled pre-compile steps, and then the toolchain
stages: synthesis, place & route, bitstream generation. **The build stops at the first stage that
fails**, and a later stage never runs on a stale result from a previous build. That means the
*first* error in the Output panel is the one to fix — later messages, if any, are consequences.

The function itself returns only success or failure; the tool output goes to the Output panel. When
it fails, ask the user for the relevant Output lines (or read the source that the error names)
rather than re-running the same build.

Compiling requires a confirmation because it runs external tools.

## Simulating a test bench

```
fpga_set_testbench              -> path, true
fpga_set_testbench_simulator    -> path, a simulator name from fpga_list_toolchains
fpga_set_testbench_option       -> path, TopModule, <module name>      (usually needed)
fpga_run_simulation             -> path
```

The per-test-bench configuration lives in a `.tbconf` file next to the test bench, not in the
project file — `fpga_get_testbench_config` reads it back.

Common options:

| Key | Simulator | Meaning |
| --- | --- | --- |
| `TopModule` | all | Name of the module the simulator elaborates. Defaults to the file name. |
| `VerilatorArguments` | Verilator | Extra compile-time arguments. |
| `VerilatorRuntimeArguments` | Verilator | Arguments passed to the built simulation binary. |
| `IcarusVerilogArguments` | Icarus Verilog | Extra arguments. |
| `WaveOutputFormat` | Icarus Verilog | `VCD` or `FST`. |

Verilator treats warnings as fatal by default, so a design that only produces warnings still fails
the run. `-Wno-fatal` in `VerilatorArguments` downgrades them; `-Wno-<CODE>` silences one specific
warning. Prefer fixing the source — a missing `` `timescale `` causing `TIMESCALEMOD` is a real
inconsistency, not noise.

## Rules

- Read the error a function returns. It names the concrete problem and usually the function to call
  next; retrying the identical call will fail identically.
- Never invent ids, board names or entity names.
- Ask before doing something the user did not ask for, especially before changing a board or a
  toolchain — that invalidates pin assignments.
