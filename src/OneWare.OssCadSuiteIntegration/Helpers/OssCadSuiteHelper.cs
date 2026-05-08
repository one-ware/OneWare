using OneWare.Essentials.PackageManager;

namespace OneWare.OssCadSuiteIntegration.Helpers;

public class OssCadSuiteHelper
{
    public const string OssPathSetting = "OssCadSuite_Path";
    public const string OpenFpgaLoaderPathSetting = "OpenFpgaLoader_Path";
    
    public static readonly Package OssCadPackage = new()
    {
        Category = "Binaries",
        Id = "osscadsuite",
        Type = "NativeTool",
        Name = "OSS CAD Suite",
        Description = "Open Source FPGA Tools",
        License = "ISC",
        IconUrl = "https://avatars.githubusercontent.com/u/35169771?s=48&v=4",
        Links = [ new PackageLink { Name = "GitHub", Url = "https://github.com/YosysHQ/oss-cad-suite-build" } ],
        Tabs = [ /* ... deine Tabs ... */ ],
        Versions = [
            CreateVersion("2024.07.27", "2024-07-27", hasLinuxArm: false),
            CreateVersion("2025.01.22", "2025-01-22", hasLinuxArm: false),
            CreateVersion("2025.08.27", "2025-08-27", hasLinuxArm: true),
            CreateVersion("2026.02.19", "2026-02-19", hasLinuxArm: true)
        ]
    };

    private static PackageVersion CreateVersion(string version, string tag, bool hasLinuxArm)
    {
        var targets = new List<PackageTarget>
        {
            CreateTarget("win-x64", "windows-x64", tag, isCustomRepo: true),
            CreateTarget("linux-x64", "linux-x64", tag),
            CreateTarget("osx-x64", "darwin-x64", tag),
            CreateTarget("osx-arm64", "darwin-arm64", tag)
        };

        if (hasLinuxArm) 
            targets.Add(CreateTarget("linux-arm64", "linux-arm64", tag));

        return new PackageVersion { Version = version, Targets = [.. targets] };
    }

    private static PackageTarget CreateTarget(string id, string osName, string tag, bool isCustomRepo = false)
    {
        string repo = isCustomRepo ? "hendrikmennen" : "YosysHQ";
        string dateClean = tag.Replace("-", "");
        return new PackageTarget
        {
            Target = id,
            Url = $"https://github.com/{repo}/oss-cad-suite-build/releases/download/{tag}/oss-cad-suite-{osName}-{dateClean}.tgz",
            AutoSetting = [ new PackageAutoSetting { RelativePath = "oss-cad-suite", SettingKey = OssPathSetting } ]
        };
    }
}