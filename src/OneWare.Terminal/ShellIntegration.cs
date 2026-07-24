using System.Runtime.InteropServices;
using System.Text;

namespace OneWare.Terminal;

/// <summary>
/// Generates the shell startup scripts and spawn configuration that install OneWare's
/// shell integration (VS Code-style). The integration makes the shell emit invisible
/// OSC 633 sequences at prompt boundaries:
///   ESC]633;C BEL          — a command has been read and is about to execute
///   ESC]633;D;&lt;exit&gt; BEL   — the command finished with the given exit code
/// Because the hooks are installed via startup files (never typed into the terminal),
/// nothing is echoed and nothing can leak into the user-facing terminal.
/// </summary>
public static class ShellIntegration
{
    public sealed record SpawnConfig(string? Arguments, string? Environment);

    private static readonly Lazy<string> ScriptDirectory = new(CreateScriptDirectory);

    public static SpawnConfig GetSpawnConfig(string shellExecutable)
    {
        var shellName = Path.GetFileNameWithoutExtension(shellExecutable).ToLowerInvariant();

        try
        {
            return shellName switch
            {
                // Note: the Unix provider splits arguments on spaces and passes them
                // verbatim to execve, so the path must not be quoted. The script lives
                // in the temp directory, which does not contain spaces on supported setups.
                "bash" => new SpawnConfig($"--init-file {WriteScript("integration.bash", BashScript)}",
                    null),
                "zsh" => CreateZshConfig(),
                "powershell" or "pwsh" => CreatePowerShellConfig(shellExecutable),
                _ => new SpawnConfig(null, null)
            };
        }
        catch (Exception)
        {
            // If the integration scripts cannot be written the terminal still works,
            // it just cannot report command completion.
            return new SpawnConfig(null, null);
        }
    }

    private static SpawnConfig CreateZshConfig()
    {
        var dir = ScriptDirectory.Value;
        WriteScript(".zshenv", ZshEnvScript);
        WriteScript(".zshrc", ZshRcScript);

        var userZdotdir = Environment.GetEnvironmentVariable("ZDOTDIR")
                          ?? Environment.GetEnvironmentVariable("HOME") ?? "~";

        return new SpawnConfig("-i", $"ZDOTDIR={dir}\0OW_USER_ZDOTDIR={userZdotdir}");
    }

    private static SpawnConfig CreatePowerShellConfig(string shellExecutable)
    {
        var script = WriteScript("integration.ps1", PowerShellScript);
        var escaped = script.Replace("'", "''");

        // The arguments string must contain the full command line because the ConPTY
        // provider uses it verbatim when it is not empty.
        var exe = Path.GetFileName(shellExecutable);
        return new SpawnConfig(
            $"{exe} -NoLogo -NoProfile -NoExit -Command \". '{escaped}'\"", null);
    }

    private static readonly Lock WriteLock = new();

    private static string WriteScript(string fileName, string content)
    {
        var path = Path.Combine(ScriptDirectory.Value, fileName);

        lock (WriteLock)
        {
            if (!File.Exists(path) || File.ReadAllText(path) != content)
            {
                // Write atomically so a shell starting in another instance/thread can
                // never observe a partially written script.
                var temp = Path.Combine(ScriptDirectory.Value, $"{fileName}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                File.Move(temp, path, true);
            }
        }

        return path;
    }

    private static string CreateScriptDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "oneware-shell-integration");
        Directory.CreateDirectory(dir);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(dir,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
            catch
            {
                // Permissions are a best effort; default umask is fine.
            }
        }

        return dir;
    }

    private const string BashScript =
        """
        # OneWare shell integration for bash.
        # Source the user's regular startup files first so their environment is intact.
        if [ -r /etc/bash.bashrc ]; then . /etc/bash.bashrc; fi
        if [ -r "$HOME/.bashrc" ]; then . "$HOME/.bashrc"; fi

        __oneware_prompt_cmd() {
            local __ow_exit=$?
            printf '\033]633;D;%s\007' "$__ow_exit"
            return $__ow_exit
        }

        # Report the exit code at every prompt (runs before anything already configured).
        PROMPT_COMMAND="__oneware_prompt_cmd${PROMPT_COMMAND:+;$PROMPT_COMMAND}"

        # PS0 is expanded after a command has been read and before it executes.
        # Note: readline markers \[ \] must NOT be used here — outside of PS1 they
        # are emitted as literal \x01/\x02 bytes.
        PS0='\e]633;C\a'"$PS0"
        """;

    private const string ZshEnvScript =
        """
        # OneWare shell integration for zsh: restore the user's ZDOTDIR files.
        OW_USER_ZDOTDIR=${OW_USER_ZDOTDIR:-$HOME}
        if [ -f "$OW_USER_ZDOTDIR/.zshenv" ]; then
            ZDOTDIR=$OW_USER_ZDOTDIR . "$OW_USER_ZDOTDIR/.zshenv"
        fi
        """;

    private const string ZshRcScript =
        """
        # OneWare shell integration for zsh.
        OW_USER_ZDOTDIR=${OW_USER_ZDOTDIR:-$HOME}
        if [ -f "$OW_USER_ZDOTDIR/.zshrc" ]; then
            ZDOTDIR=$OW_USER_ZDOTDIR . "$OW_USER_ZDOTDIR/.zshrc"
        fi

        __oneware_preexec() {
            printf '\033]633;C\007'
        }

        __oneware_precmd() {
            printf '\033]633;D;%s\007' "$?"
        }

        autoload -Uz add-zsh-hook
        add-zsh-hook preexec __oneware_preexec
        add-zsh-hook precmd __oneware_precmd
        """;

    private const string PowerShellScript =
        """
        # OneWare shell integration for PowerShell.
        $Global:__OneWareOriginalPrompt = $function:Prompt

        function Global:Prompt() {
            $__ow_success = $?
            $__ow_exit = if ($__ow_success) { 0 }
                elseif ($Global:LASTEXITCODE -is [int] -and $Global:LASTEXITCODE -ne 0) { $Global:LASTEXITCODE }
                else { 1 }
            $__ow_out = "$([char]0x1b)]633;D;$__ow_exit$([char]0x07)"
            $__ow_out += if ($Global:__OneWareOriginalPrompt) { $Global:__OneWareOriginalPrompt.Invoke() } else { "PS $PWD> " }
            return $__ow_out
        }

        # Emit the command-started marker once a line has been read, before it executes.
        if (Get-Module -Name PSReadLine) {
            function Global:PSConsoleHostReadLine {
                $__ow_line = [Microsoft.PowerShell.PSConsoleReadLine]::ReadLine($Host.Runspace, $ExecutionContext)
                [Console]::Write("$([char]0x1b)]633;C$([char]0x07)")
                return $__ow_line
            }
        }
        """;
}
