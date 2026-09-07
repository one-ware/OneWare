---
name: fpga-toolchain-yosys
description: The Yosys/nextpnr toolchain in ONE WARE Studio — which device setting key controls which stage of the compile, what the build produces, and how to read a failed synthesis, place & route or bitstream run.
---

# The Yosys toolchain

The `yosys` toolchain compiles an FPGA project in three stages that run in this order:

1. **Synthesis** — `yosys` reads the HDL sources and writes `build/synth.json`.
2. **Place & Route** — a `nextpnr-*` binary reads `build/synth.json` and the constraint file and
   writes the placed and routed design into `build/`.
3. **Bitstream** — a pack tool (`icepack`, `ecppack`, `gowin_pack`, `gmpack`) turns that into the
   bitstream in `build/`.

Everything is written below `build/` in the project folder. That folder is in the project's
`exclude` list, so it does not show up as project content.

**A stage only runs when the previous one succeeded.** Each stage deletes its own stale output
before it starts and verifies that the expected artifact exists afterwards, so a build can never
silently reuse a result from an earlier run. This matters when reading a failed compile: the first
reported error is the real one, and there is no risk that a "successful" place & route was in fact
routing yesterday's netlist.

## Configuring the stages

These are **device settings** of the selected board, stored in
`device-settings/<board>.deviceconf`. Read them with `fpga_list_device_settings` and change them
with `fpga_set_device_setting`. The same values are what the *Toolchain Settings* dialog under
Compile shows, grouped into Synthesis, Place & Route and Bitstream.

### Synthesis (Yosys)

| Key | Meaning |
| --- | --- |
| `yosysToolchainYosysSynthTool` | The `synth_*` command for the target family, e.g. `synth_ice40`, `synth_ecp5`, `synth_gowin`. Must match the board. |
| `yosysToolchainYosysFlags` | Extra flags passed to `yosys`. |
| `yosysToolchainCommand` | Overrides the generated yosys script command entirely. Leave empty unless the default pipeline does not fit. |
| `yosysQuietFlag` | `true` suppresses the yosys output. The dialog exposes the inverse as *Yosys Verbose*. |

### Place & Route (nextpnr)

| Key | Meaning |
| --- | --- |
| `yosysToolchainNextPnrTool` | The `nextpnr-*` binary, e.g. `nextpnr-ice40`, `nextpnr-ecp5`, `nextpnr-himbaechel`. Must match the synth tool's family. |
| `yosysToolchainNextPnrFlags` | Extra flags, typically the device and package selection. |
| `yosysToolchainConstraintFileType` | `pcf` or `ccf` — the constraint format the board uses. |
| `yosysToolchainOutputType` | `asc` or `txt` — the place & route output format the pack tool expects. |
| `yosysToolchainNextPnrVerbose` | `true` adds `--verbose` and shows the full nextpnr output in the Output panel. Turn this on when diagnosing timing or placement problems. |

### Bitstream (pack)

| Key | Meaning |
| --- | --- |
| `yosysToolchainPackTool` | `icepack`, `ecppack`, `gowin_pack` or `gmpack`, matching the family. |
| `yosysToolchainPackFlags` | Extra flags for the pack tool. |
| `packToolOutputFormat` | `bin` or `bit`. |

The three tool selections must agree on the FPGA family: `synth_ice40` + `nextpnr-ice40` +
`icepack` is consistent, mixing families is not. The hardware package of a board ships correct
defaults, so only change them when the user targets a device the package does not cover.

## Reading a failed compile

The compile output goes to the Output panel, prefixed with the project name. A tool that exits with
a non-zero status, or that prints a line starting with `Error:` / `ERROR:`, fails the stage and
stops the build.

| Symptom | Cause |
| --- | --- |
| `ERROR: Module ... not found` from yosys | A source file is missing from the project or is in `compileExcluded`. |
| `ERROR: Can't open input file` from yosys | The top entity name does not match any module. Check `fpga_list_top_entities`. |
| `Cannot run place & route: build/synth.json is missing` | Synthesis did not produce a netlist — fix the synthesis error above it. |
| nextpnr reports unconstrained or unknown pins | The constraint file does not match the design. Assign the pins in the pin planner. |
| `... exited with code N` with no other error | The tool failed without a parseable message; the verbose option for that stage usually reveals why. |

Do not re-run an identical build to "see if it works this time". The stages are deterministic and a
stale artifact can no longer mask a failure.

## Programming

After a successful build the loader writes the bitstream to the hardware:

- **Config FPGA** loads the bitstream into the FPGA's volatile configuration. It is gone after a
  power cycle.
- **Write FLASH Memory** writes it into the board's FLASH, so the FPGA loads it on every power-up.

Both need a bitstream from a completed build; the loader refuses to start without one.
