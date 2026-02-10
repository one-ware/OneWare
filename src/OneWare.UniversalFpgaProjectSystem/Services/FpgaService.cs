using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;
using OneWare.Essentials.Extensions;
using OneWare.Essentials.Models;
using OneWare.Essentials.Services;
using OneWare.UniversalFpgaProjectSystem.Fpga;
using OneWare.UniversalFpgaProjectSystem.Models;

namespace OneWare.UniversalFpgaProjectSystem.Services;

public class FpgaService
{
    private readonly ILogger _logger;
    private readonly ISettingsService _settingsService;
    private readonly IWindowService _windowService;

    public FpgaService(IPaths paths, ILogger logger, ISettingsService settingsService, IWindowService windowService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _windowService = windowService;

        HardwareDirectory = Path.Combine(paths.PackagesDirectory, "Hardware");
        Directory.CreateDirectory(HardwareDirectory);

        //Create Local
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local"));
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local", "FPGA"));
        Directory.CreateDirectory(Path.Combine(HardwareDirectory, "Local", "Extensions"));

        LoadGenericHardware();
    }

    public string HardwareDirectory { get; }

    // Key: Language Name, Value: File Extensions
    public Dictionary<string, string[]> FpgaLanguages { get; } = new();

    public ObservableCollection<IFpgaPackage> FpgaPackages { get; } = new();

    public ObservableCollection<IFpgaExtensionPackage> FpgaExtensionPackages { get; } = new();

    public ObservableCollection<IFpgaToolchain> Toolchains { get; } = new();

    public ObservableCollection<IFpgaLoader> Loaders { get; } = new();

    public ObservableCollection<IFpgaSimulator> Simulators { get; } = new();

    public ObservableCollection<IFpgaProjectTemplate> Templates { get; } = new();

    public ObservableCollection<IFpgaPreCompileStep> PreCompileSteps { get; } = new();

    public ObservableCollection<INodeProvider> NodeProviders { get; } = new();

    public ObservableCollection<Action<IProjectEntry>> ProjectEntryModificationHandlers { get; } = new();

    public IList<ProjectPropertyMigration> ProjectPropertyMigrations { get; } = new List<ProjectPropertyMigration>();

    public void RegisterProjectEntryModification(Action<IProjectEntry> modificationAction)
    {
        ProjectEntryModificationHandlers.Add(modificationAction);
    }

    public void RegisterProjectPropertyMigration(ProjectPropertyMigration migration)
    {
        ProjectPropertyMigrations.Add(migration);
    }

    public void RegisterProjectPropertyMigration(
        string fromPath,
        string toPath,
        Func<JsonNode?, JsonNode?>? transform = null)
    {
        ProjectPropertyMigrations.Add(new ProjectPropertyMigration(fromPath, toPath, transform));
    }

    public void RegisterLanguage(string language, string[] extensions)
    {
        FpgaLanguages[language] = extensions;
    }

    public void RegisterFpgaPackage(IFpgaPackage fpga)
    {
        var existing = FpgaPackages.FirstOrDefault(x => x.Name == fpga.Name);

        if (existing != null) FpgaPackages.Remove(existing);

        FpgaPackages.InsertSorted(fpga, (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterFpgaExtensionPackage(IFpgaExtensionPackage fpgaExtension)
    {
        var existing = FpgaExtensionPackages.FirstOrDefault(x => x.Name == fpgaExtension.Name);

        if (existing != null) FpgaExtensionPackages.Remove(existing);

        FpgaExtensionPackages.InsertSorted(fpgaExtension,
            (x1, x2) => string.Compare(x1.Name, x2.Name, StringComparison.Ordinal));
    }

    public void RegisterToolchain<T>() where T : IFpgaToolchain
    {
        Toolchains.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterLoader<T>() where T : IFpgaLoader
    {
        Loaders.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterSimulator<T>() where T : IFpgaSimulator
    {
        Simulators.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterTemplate<T>() where T : IFpgaProjectTemplate
    {
        Templates.Add(ContainerLocator.Container.Resolve<T>());
    }

    public void RegisterNodeProvider<T>() where T : INodeProvider
    {
        var nodeProvider = ContainerLocator.Container.Resolve<T>();
        NodeProviders.Add(nodeProvider);

        foreach (var language in nodeProvider.SupportedLanguages) RegisterNodeProviderSetting(language);
    }

    private void RegisterNodeProviderSetting(string language)
    {
        var settingKey = $"UniversalFPGA_NodeProviderPreference_{language}";

        var possibleNodeProviders = NodeProviders
            .Where(x => x.SupportedLanguages.Contains(language))
            .ToList();

        var options = possibleNodeProviders
            .Select(x => x.Name)
            .ToList();

        if (_settingsService.HasSetting(settingKey))
        {
            (_settingsService.GetSetting(settingKey) as ComboBoxSetting)!.Options = options.ToArray();
        }
        else
        {
            var nodeProviderComboSetting =
                new ComboBoxSetting($"{language} Node Provider", options.FirstOrDefault() ?? "", options.ToArray())
                {
                    MarkdownDocumentation =
                        $"Node Provider used to extract FPGA nodes from {language} files. "
                };

            _settingsService.RegisterSetting("Languages", language, settingKey, nodeProviderComboSetting);
        }
    }

    public void RegisterPreCompileStep<T>() where T : IFpgaPreCompileStep
    {
        PreCompileSteps.Add(ContainerLocator.Container.Resolve<T>());
    }

    public IFpgaPreCompileStep? GetPreCompileStep(string name)
    {
        return PreCompileSteps.FirstOrDefault(x => x.Name == name);
    }

    public string? GetLanguage(string extension)
    {
        var language = FpgaLanguages.FirstOrDefault(x => x.Value.Contains(extension));

        return language.Key;
    }

    public INodeProvider? GetNodeProviderByExtension(string extension)
    {
        if (GetLanguage(extension) is { } language)
            return GetNodeProvider(language);

        return null;
    }

    public INodeProvider? GetNodeProvider(string language)
    {
        var possibleNodeProviders = NodeProviders
            .Where(x => x.SupportedLanguages.Contains(language))
            .ToList();

        var settingKey = $"UniversalFPGA_NodeProviderPreference_{language}";

        if (_settingsService.HasSetting(settingKey))
        {
            var selectedNodeProviderName = _settingsService.GetSettingValue<string>(settingKey);

            var selectedNodeProvider = possibleNodeProviders
                .FirstOrDefault(x => x.Name.Equals(selectedNodeProviderName, StringComparison.OrdinalIgnoreCase));

            return selectedNodeProvider ?? possibleNodeProviders.FirstOrDefault();
        }

        return possibleNodeProviders.FirstOrDefault();
    }

    public void LoadGenericHardware()
    {
        foreach (var fpga in FpgaPackages.ToArray())
            if (fpga is GenericFpgaPackage)
                FpgaPackages.Remove(fpga);

        foreach (var extension in FpgaExtensionPackages.ToArray())
            if (extension is GenericFpgaExtensionPackage gen && !gen.PackagePath.StartsWith("avares://"))
                FpgaExtensionPackages.Remove(extension);

        try
        {
            foreach (var packageDir in Directory.GetDirectories(HardwareDirectory)
                         .OrderBy(x => x != Path.Combine(HardwareDirectory, "Local")))
            {
                var fpgaDir = Path.Combine(packageDir, "FPGA");

                if (Directory.Exists(fpgaDir))
                    foreach (var fpga in Directory.GetDirectories(fpgaDir))
                        RegisterFpgaPackage(new GenericFpgaPackage(Path.GetFileName(fpga), fpga));

                var extensionDir = Path.Combine(packageDir, "Extensions");

                if (Directory.Exists(extensionDir))
                    foreach (var connector in Directory.GetDirectories(extensionDir))
                    foreach (var extension in Directory.GetDirectories(connector))
                        RegisterFpgaExtensionPackage(new GenericFpgaExtensionPackage(Path.GetFileName(extension),
                            Path.GetFileName(connector), extension));
            }
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
        }
    }

    public async Task<bool> RunToolchainAsync(UniversalFpgaProjectRoot project, FpgaModel? fpgaModel = null)
    {
        try
        {
            var toolchain = Toolchains.FirstOrDefault(x => x.Id == project.Toolchain);

            if (toolchain == null)
            {
                ContainerLocator.Container.Resolve<ILogger>().Warning($"Toolchain {project.Toolchain} not found");
                return false;
            }

            if (fpgaModel == null)
            {
                var name = project.Properties.GetString("fpga");
                var fpgaPackage = FpgaPackages.FirstOrDefault(obj => obj.Name == name);
                if (fpgaPackage == null)
                {
                    ContainerLocator.Container.Resolve<ILogger>().Warning($"No FPGA Selected (or {name} not found). Open Pin Planner first");
                    return false;
                }
                fpgaModel = new FpgaModel(fpgaPackage.LoadFpga());
            }

            foreach (var step in PreCompileSteps)
                if (!await step.PerformPreCompileStepAsync(project, fpgaModel))
                    return false;
            
            await toolchain.CompileAsync(project, fpgaModel);
            return true;
        }
        catch (Exception e)
        {
            _logger.Error(e.Message, e);
            return false;
        }
    }
}
