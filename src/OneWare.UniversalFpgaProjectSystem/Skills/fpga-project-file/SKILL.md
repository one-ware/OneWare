---
name: fpga-project-file
description: Format of a ONE WARE Studio .fpgaproj file and of the .deviceconf and .tbconf files next to it — which key means what, which ones have a dedicated fpga_* function, and which ones may only be edited by hand.
---

# The .fpgaproj file and its companions

An FPGA project is a folder containing one `<name>.fpgaproj` file. That file is plain JSON and is
the single source of truth for the project; the folder layout carries no meaning beyond it.

Prefer the `fpga_*` functions over editing these files. They validate the value against what is
actually installed, keep the in-memory project model in sync, and save. Hand-editing a project file
that the IDE has open loses the edit on the next save.

## .fpgaproj

```jsonc
{
  "include": ["*.vhd", "*.vhdl", "*.v", "*.vcd", "vhdl_ls.toml"],
  "exclude": ["build"],
  "toolchain": "yosys",
  "loader": "openFpgaLoader",
  "board": "MyBoard",
  "topEntity": "top",
  "compileExcluded": ["src/old_top.v"],
  "testBenches": ["sim/top_tb.v"],
  "preCompileSteps": ["someStepId"]
}
```

| Key | Meaning | Function |
| --- | --- | --- |
| `include` | Glob patterns or relative paths that make up the project file set. It must list at least one pattern — an empty or missing array includes **no** file, so the project appears empty and nothing reaches the toolchain. | `fpga_set_project_setting` (`include`) |
| `exclude` | Patterns removed from the file set again. Excluded files are invisible to the IDE. | `fpga_set_project_setting` (`exclude`) |
| `toolchain` | Id of the compile toolchain. | `fpga_set_toolchain` |
| `loader` | Id of the programmer used to configure the FPGA or write its FLASH. | `fpga_set_loader` |
| `board` | Name of the selected board. | `fpga_set_board` |
| `topEntity` | Name of the top level entity/module for synthesis. | `fpga_set_top_entity` |
| `compileExcluded` | Relative paths kept in the project but not passed to synthesis. | `fpga_set_compile_excluded` |
| `testBenches` | Relative paths marked as test benches. | `fpga_set_testbench` |
| `preCompileSteps` | Ids of the pre-compile steps that run before synthesis. | `fpga_set_project_setting` (`preCompileStep_<id>`) |

Everything else in the file was registered by a plugin — typically the toolchain — and appears in
`fpga_list_project_settings` with its key, type and allowed values. `vhdlStandard` is one example;
which keys exist depends on the selected toolchain, so list them instead of assuming.

Notes on the format:

- Paths are relative to the project folder and use `/` as separator, on every platform. A pattern
  written with `\` never matches.
- `include`/`exclude` decide what the IDE sees at all. `compileExcluded` decides what the
  *synthesizer* sees. They are not interchangeable: a file removed via `exclude` disappears from
  the project explorer, a `compileExcluded` file stays visible and greyed out.
- `fpga` was the old key for `board`. Old files are migrated automatically; write `board`.
- `Toolchain`, `Loader` and `VHDL_Standard` (capitalised) are likewise legacy spellings that are
  migrated to `toolchain`, `loader` and `vhdlStandard`.

## device-settings/&lt;board&gt;.deviceconf

A flat JSON object of string properties describing the selected board, for example a device part
number or a programming mode:

```json
{
  "device": "iCE40UP5K",
  "package": "sg48"
}
```

The defaults come from the installed hardware package; the file only needs to carry the values that
differ. An entry with an empty value is dropped on save, which is how "use the board default" is
expressed — `fpga_set_device_setting` with an empty value does exactly that.

The file is only meaningful once a board is selected, and it is per board: switching the board
switches to a different `.deviceconf`.

## &lt;testbench&gt;.tbconf

Simulation configuration for one test bench, stored next to it with the same base name:

```json
{
  "simulator": "Verilator",
  "TopModule": "top_tb",
  "VerilatorArguments": "-Wno-fatal"
}
```

`simulator` names the simulator; every other key is an option of that simulator. Read it with
`fpga_get_testbench_config`, write it with `fpga_set_testbench_simulator` and
`fpga_set_testbench_option`.

A `.tbconf` is independent of the `testBenches` array in the project file: the array controls the
test bench marker and toolbar in the IDE, the `.tbconf` controls how the file is simulated. In
practice you want both.
