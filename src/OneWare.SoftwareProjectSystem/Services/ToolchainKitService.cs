using System.Runtime.InteropServices;
using OneWare.Essentials.Helpers;
using OneWare.SoftwareProjectSystem.Models;

namespace OneWare.SoftwareProjectSystem.Services;

/// <summary>
/// Knows the toolchain kits available for software projects. Host GCC / Clang and common cross compilers are
/// detected on PATH; plugins and packages add more with <see cref="RegisterKit" />.
/// </summary>
public class ToolchainKitService
{
    private static readonly string[] CrossPrefixes =
    [
        "arm-none-eabi",
        "riscv-none-elf",
        "riscv32-unknown-elf",
        "riscv64-unknown-elf",
        "riscv32-esp-elf",
        "xtensa-esp32-elf"
    ];

    private readonly List<ToolchainKit> _detectedKits = [];
    private readonly List<ToolchainKit> _registeredKits = [];
    private readonly Func<string, string?> _resolveExecutable;
    private bool _detected;

    public ToolchainKitService() : this(name => PlatformHelper.GetFullPath(name + PlatformHelper.ExecutableExtension))
    {
    }

    public ToolchainKitService(Func<string, string?> resolveExecutable)
    {
        _resolveExecutable = resolveExecutable;
    }

    /// <summary>
    /// Registered kits first, then detected ones.
    /// </summary>
    public IReadOnlyList<ToolchainKit> Kits
    {
        get
        {
            if (!_detected) Refresh();
            return _registeredKits.Concat(_detectedKits.Where(d => _registeredKits.All(r => r.Id != d.Id)))
                .ToArray();
        }
    }

    public event EventHandler? KitsChanged;

    public void RegisterKit(ToolchainKit kit)
    {
        _registeredKits.RemoveAll(x => x.Id == kit.Id);
        _registeredKits.Add(kit);
        KitsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Searches PATH again, e.g. after a toolchain was installed.
    /// </summary>
    public void Refresh()
    {
        _detected = true;
        _detectedKits.Clear();

        var exeExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

        if (_resolveExecutable("gcc") is { } gcc && _resolveExecutable("g++") is { } gxx)
            _detectedKits.Add(new ToolchainKit("host-gcc", "GCC (Host)", gcc, gxx)
            {
                Gdb = _resolveExecutable("gdb"),
                ExecutableExtension = exeExtension
            });

        if (_resolveExecutable("clang") is { } clang && _resolveExecutable("clang++") is { } clangxx)
            _detectedKits.Add(new ToolchainKit("host-clang", "Clang (Host)", clang, clangxx)
            {
                Family = CompilerFamily.Clang,
                Gdb = _resolveExecutable("gdb") ?? _resolveExecutable("lldb"),
                ExecutableExtension = exeExtension
            });

        foreach (var prefix in CrossPrefixes)
        {
            if (_resolveExecutable($"{prefix}-gcc") is not { } cc ||
                _resolveExecutable($"{prefix}-g++") is not { } cxx) continue;

            _detectedKits.Add(new ToolchainKit(prefix, $"GCC ({prefix})", cc, cxx)
            {
                IsHost = false,
                Target = prefix,
                Gdb = _resolveExecutable($"{prefix}-gdb") ?? _resolveExecutable("gdb-multiarch"),
                ExecutableExtension = ".elf"
            });
        }

        KitsChanged?.Invoke(this, EventArgs.Empty);
    }

    public ToolchainKit? GetKit(string? id)
    {
        return id == null ? null : Kits.FirstOrDefault(x => x.Id == id);
    }

    /// <summary>
    /// The kit selected in the project, or the first host kit if none (or an unknown one) is selected.
    /// </summary>
    public ToolchainKit? GetKitForProject(SoftwareProjectRoot project)
    {
        return GetKit(project.Kit) ?? (project.Kit == null ? Kits.FirstOrDefault(x => x.IsHost) : null);
    }
}
