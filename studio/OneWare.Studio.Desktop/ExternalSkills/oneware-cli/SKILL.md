---
name: oneware-cli
description: Use the OneWare Studio command-line interface to discover commands, manage extensions, synchronize agent skills, sign in to OneWare Cloud, or stop Studio.
---

# OneWare Studio CLI

OneWare Studio provides a headless CLI. Use `oneware` in a terminal; on Windows, `oneware.exe`
is the console executable. If the command is not on PATH, locate the installed executable rather
than assuming a location. Run `oneware --help` to discover the commands available in this
installation, then use `oneware <command> --help` for current arguments and options. Extensions
can contribute additional commands, so do not assume every installation has the same commands.
Running `oneware` without a command starts the Studio GUI.

## Built-in commands

- `oneware extensions search [query]` searches the extension catalog.
- `oneware extensions list` lists installed extensions.
- `oneware extensions install <id>` installs an extension; use `--version <version>` when needed.
- `oneware extensions update [id]` updates one extension, or all installed extensions if no ID is given.
- `oneware extensions uninstall <id>` removes an extension.
- `oneware agent init` synchronizes external agent skills into `~/.agents/skills/`.
- `oneware cloud login` opens a browser to sign in; `oneware cloud logout` signs out.
- `oneware stop` stops the running Studio.

Extension catalog operations can require network access. If the extension ID is unknown, search or list
extensions rather than guessing it. Do not bypass compatibility checks unless explicitly requested.
Ask before installing, updating or uninstalling extensions, synchronizing skills, signing in or out,
or stopping Studio unless the user already requested the action. An update without an ID affects all
installed extensions.

`agent init` writes skills and may remove unchanged skills from uninstalled extensions or omitted
`--modules` directories. It preserves unmanaged or locally modified skills and reports conflicts.
Rerun it after changing extensions to synchronize their external skills.

The CLI can also have extension-provided commands such as `oneware oneai`. Discover them with
`oneware --help` and inspect their help before use; they may not be installed.
