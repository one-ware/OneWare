using System.Runtime.InteropServices;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OneWare.Essentials.LanguageService;
using OneWare.Essentials.Services;
using OneWare.Essentials.ViewModels;

namespace OneWare.CSharp;

public class LanguageServiceCSharp : LanguageServiceLsp
{
    public LanguageServiceCSharp(string workspace, ISettingsService settingsService)
        : base(CSharpModule.LspName, workspace)
    {
        settingsService.GetSettingObservable<string>(CSharpModule.LspPathSetting)
            .Subscribe(x => { ExecutablePath = x; });
    }

    public override IReadOnlyCollection<KeyValuePair<string, string>> GetExtraEnvironmentVariables()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return base.GetExtraEnvironmentVariables();

        var pathSeparator = Path.PathSeparator;
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        var merged = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            foreach (var entry in currentPath.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                if (seen.Add(entry)) merged.Add(entry);
            }
        }

        var candidates = new List<string>();

        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            candidates.Add(dotnetRoot);
            candidates.Add(Path.Combine(dotnetRoot, "tools"));
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            candidates.Add(Path.Combine(home, ".dotnet"));
            candidates.Add(Path.Combine(home, ".dotnet", "tools"));
            candidates.Add(Path.Combine(home, ".local", "bin"));
        }

        candidates.Add("/usr/local/bin");
        candidates.Add("/usr/bin");
        candidates.Add("/usr/local/share/dotnet");
        candidates.Add("/usr/share/dotnet");

        foreach (var candidate in candidates)
        {
            if (!Directory.Exists(candidate)) continue;
            if (seen.Add(candidate)) merged.Add(candidate);
        }

        var newPath = string.Join(pathSeparator, merged);
        return
        [
            new KeyValuePair<string, string>("PATH", newPath)
        ];
    }

    public override ITypeAssistance GetTypeAssistance(IEditor editor)
    {
        return new TypeAssistanceCSharp(editor, this);
    }
}
