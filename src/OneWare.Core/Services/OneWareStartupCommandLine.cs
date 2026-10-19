using System.CommandLine;

namespace OneWare.Core.Services;

public static class OneWareStartupCommandLine
{
    public static RootCommand CreateRootCommand(OneWareStartupSymbols symbols)
    {
        return new RootCommand
        {
            Options =
            {
                symbols.DirOption,
                symbols.AppdataDirOption,
                symbols.ProjectsDirOption,
                symbols.ModuleOption,
                symbols.AutoLaunchOption,
                symbols.PackageRepositoryOption,
                symbols.ConfigurationProfileOption,
                symbols.WorkingDirectoryOption
            },
            Arguments =
            {
                symbols.OpenArgument
            }
        };
    }

    public static OneWareStartupSymbols CreateSymbols(
        string openArgumentDescription = "File/Folder path or oneware:// URI to open")
    {
        return new OneWareStartupSymbols(
            new Option<string>("--oneware-dir") { Description = "Path to documents directory for OneWare Studio. (optional)" },
            new Option<string>("--oneware-projects-dir") { Description = "Path to default projects directory for OneWare Studio. (optional)" },
            new Option<string>("--oneware-appdata-dir") { Description = "Path to application data directory for OneWare Studio. (optional)" },
            new Option<string>("--modules") { Description = "Adds plugin to OneWare Studio during initialization. (optional)" },
            new Option<string>("--autolaunch") { Description = "Auto launches a specific action after OneWare Studio is loaded. Can be used by plugins (optional)" },
            new Option<string>("--package-repository") { Description = "Overrides the package repository URL(s) used by OneWare Studio. Separate multiple URLs with ';'. (optional)" },
            new Option<string>("--configuration-profile") { Description = "Applies a configuration profile (settings, packages, package sources) at startup. Accepts a file path or an http(s) URL. (optional)" },
            new Option<string>("--working-directory", "-C") { Description = "Changes the working directory for this command." },
            new Argument<string?>("open") { Description = openArgumentDescription, DefaultValueFactory = _ => null });
    }

    public static void ApplyEnvironmentVariables(ParseResult parseResult, OneWareStartupSymbols symbols)
    {
        SetWorkingDirectory(parseResult.GetValue(symbols.WorkingDirectoryOption));
        SetPathEnvironmentVariable("ONEWARE_DIR", parseResult.GetValue(symbols.DirOption));
        SetPathEnvironmentVariable("ONEWARE_PROJECTS_DIR", parseResult.GetValue(symbols.ProjectsDirOption));
        SetPathEnvironmentVariable("ONEWARE_APPDATA_DIR", parseResult.GetValue(symbols.AppdataDirOption));
        SetEnvironmentVariable("ONEWARE_MODULES", parseResult.GetValue(symbols.ModuleOption));
        SetEnvironmentVariable("ONEWARE_AUTOLAUNCH", parseResult.GetValue(symbols.AutoLaunchOption));
        SetEnvironmentVariable("ONEWARE_PACKAGE_REPOSITORY", parseResult.GetValue(symbols.PackageRepositoryOption));
        SetEnvironmentVariable("ONEWARE_CONFIGURATION_PROFILE", parseResult.GetValue(symbols.ConfigurationProfileOption));

        var openValue = parseResult.GetValue(symbols.OpenArgument);
        if (string.IsNullOrEmpty(openValue))
            return;

        if (openValue.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("ONEWARE_OPEN_URL", openValue);
        else if (File.Exists(openValue) || Directory.Exists(openValue))
            Environment.SetEnvironmentVariable("ONEWARE_OPEN_PATH", Path.GetFullPath(openValue));
    }

    public static bool ContainsOneWareUriArgument(IEnumerable<string> args)
    {
        return args.Any(x => x.StartsWith("oneware://", StringComparison.OrdinalIgnoreCase));
    }

    private static void SetPathEnvironmentVariable(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            Environment.SetEnvironmentVariable(key, Path.GetFullPath(value));
    }

    private static void SetEnvironmentVariable(string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            Environment.SetEnvironmentVariable(key, value);
    }

    private static void SetWorkingDirectory(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var path = Path.GetFullPath(value);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"The working directory '{path}' does not exist.");

        Environment.CurrentDirectory = path;
    }
}

public sealed record OneWareStartupSymbols(
    Option<string> DirOption,
    Option<string> ProjectsDirOption,
    Option<string> AppdataDirOption,
    Option<string> ModuleOption,
    Option<string> AutoLaunchOption,
    Option<string> PackageRepositoryOption,
    Option<string> ConfigurationProfileOption,
    Option<string> WorkingDirectoryOption,
    Argument<string?> OpenArgument);
